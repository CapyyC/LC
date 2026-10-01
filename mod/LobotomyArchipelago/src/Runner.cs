using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Persistent MonoBehaviour: processes client messages, applies items, sweeps locations, paces traps and draws a
    /// small status overlay (F8 toggles it).
    /// </summary>
    public class Runner : MonoBehaviour
    {
        public static bool InManagement;
        public static float ManagementStartTime;

        private const float TrapInterval = 20f;
        private const float DeathLinkGrace = 10f;

        private readonly List<KeyValuePair<float, string>> _messages = new List<KeyValuePair<float, string>>();
        private readonly List<KeyValuePair<float, Dictionary<string, object>>> _pendingDeathLinks =
            new List<KeyValuePair<float, Dictionary<string, object>>>();
        private float _nextTick;
        private float _nextFlush;
        private float _nextSave;
        private float _nextReconcile;
        private float _lastTrap;
        private static float _lastDeathLinkReceived = -100f;
        private bool _showOverlay = true;
        private GUIStyle _style;
        private readonly List<string> _slotWarnings = new List<string>();

        public void AddMessage(string text)
        {
            _messages.Add(new KeyValuePair<float, string>(Time.realtimeSinceStartup, text));
            if (_messages.Count > 8) _messages.RemoveAt(0);
        }

        public void FlushChecksSoon()
        {
            _nextFlush = 0f;
        }

        private void Update()
        {
            try
            {
                Bridge bridge = Plugin.Bridge;
                if (bridge != null)
                {
                    if (bridge.ConsumeJustConnected())
                    {
                        bridge.Send(new Dictionary<string, object>
                        {
                            { "cmd", "Hello" }, { "version", Plugin.Version }, { "protocol", Plugin.ProtocolVersion }
                        });
                    }
                    Dictionary<string, object> message;
                    int handled = 0;
                    while (handled < 50 && bridge.TryDequeue(out message))
                    {
                        HandleMessage(message);
                        handled++;
                    }
                }

                HandleHotkeys();

                float now = Time.realtimeSinceStartup;
                if (now >= _nextTick)
                {
                    _nextTick = now + 1f;
                    Tick(now);
                }
                if (now >= _nextFlush)
                {
                    _nextFlush = now + 2f;
                    FlushChecks();
                }
                if (now >= _nextSave && Plugin.Session.Dirty)
                {
                    _nextSave = now + 5f;
                    Plugin.Session.Save();
                }
            }
            catch (Exception e)
            {
                Log.Error("Runner update failed", e);
            }
        }

        private void Tick(float now)
        {
            Session session = Plugin.Session;
            ItemApplier.ProcessNewItems();
            if (!session.HasSlot) return;

            bool inFacility = GameApi.InFacility;
            if (!inFacility) InManagement = false;
            if (!inFacility) return;

            Tracker.Sweep();
            ItemApplier.ApplyPendingFillers(InManagement);
            if (now >= _nextReconcile)
            {
                _nextReconcile = now + 5f;
                ItemApplier.ReconcileState();
                WarnAboutAgentSlots(false);
            }

            if (InManagement && now - ManagementStartTime > 10f && now - _lastTrap > TrapInterval &&
                session.PendingTraps.Count > 0)
            {
                _lastTrap = now;
                ItemApplier.FireNextTrap();
            }

            for (int i = 0; i < _pendingDeathLinks.Count; i++)
            {
                KeyValuePair<float, Dictionary<string, object>> entry = _pendingDeathLinks[i];
                if (now - entry.Key > 60f)
                {
                    _pendingDeathLinks.RemoveAt(i--);
                    continue;
                }
                if (!InManagement) continue;
                _pendingDeathLinks.RemoveAt(i--);
                Traps.DeathLinkEffect(session.DeathLinkEffect, entry.Value.Str("source", "someone"));
            }

            if (!session.GoalReported) Tracker.CheckGoal();
        }

        public void FlushChecks()
        {
            Bridge bridge = Plugin.Bridge;
            Session session = Plugin.Session;
            if (bridge == null || !bridge.Connected || !session.ServerConnected) return;
            List<long> unsent = session.UnsentChecks();
            if (unsent.Count == 0) return;
            var locations = new List<object>();
            foreach (long location in unsent) locations.Add(location);
            bridge.Send(new Dictionary<string, object> { { "cmd", "LocationChecks" }, { "locations", locations } });
        }

        private void HandleMessage(Dictionary<string, object> message)
        {
            Session session = Plugin.Session;
            string cmd = message.Str("cmd");
            switch (cmd)
            {
                case "Connected":
                    session.OnConnected(message);
                    AddMessage("Connected to Archipelago as " + session.SlotName);
                    if (session.GoalReported) Tracker.CheckGoal();
                    _nextFlush = 0f;
                    break;
                case "ReceivedItems":
                    if (!session.OnReceivedItems(message))
                    {
                        Plugin.Bridge.Send(new Dictionary<string, object> { { "cmd", "Sync" } });
                    }
                    break;
                case "LocationsChecked":
                    session.OnLocationsChecked(message);
                    break;
                case "Print":
                    Plugin.Message(message.Str("text"));
                    break;
                case "DeathLink":
                    if (!session.DeathLink) break;
                    _lastDeathLinkReceived = Time.realtimeSinceStartup;
                    AddMessage("DeathLink from " + message.Str("source") + ": " + message.Str("cause"));
                    _pendingDeathLinks.Add(new KeyValuePair<float, Dictionary<string, object>>(
                        Time.realtimeSinceStartup, message));
                    break;
                case "Disconnected":
                    session.ServerConnected = false;
                    AddMessage("The client lost the connection to the Archipelago server");
                    break;
            }
        }

        public static void SendDeathLink(string cause)
        {
            if (Time.realtimeSinceStartup - _lastDeathLinkReceived < DeathLinkGrace) return;
            Bridge bridge = Plugin.Bridge;
            if (bridge == null || !bridge.Connected) return;
            bridge.Send(new Dictionary<string, object> { { "cmd", "DeathLink" }, { "cause", cause } });
            Plugin.Message("DeathLink sent.");
        }

        public static void WarnAboutAgentSlots(bool announce)
        {
            Runner runner = Plugin.Runner;
            Session session = Plugin.Session;
            if (runner == null) return;
            runner._slotWarnings.Clear();
            if (!session.HasSlot || !session.AgentSlotsRandomized) return;
            foreach (object sefira in GameApi.ActivatedSefiras())
            {
                int sefiraEnum = GameApi.SefiraEnumOf(sefira);
                if (sefiraEnum < 0 || sefiraEnum > (int)SefiraIds.KETHER) continue;
                int count = 0;
                foreach (object agent in GameApi.AgentsOf(sefira))
                {
                    if (!GameApi.IsDead(agent)) count++;
                }
                int cap = session.AgentSlotCap(sefiraEnum);
                if (count <= cap) continue;
                string warning = (Reflect.Get(sefira, "name") ?? Enum.GetName(typeof(SefiraIds), sefiraEnum)) +
                                 ": " + count + " agents but only " + cap + " slot(s)";
                runner._slotWarnings.Add(warning);
                if (announce) Plugin.Message("Too many agents! " + warning);
            }
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown("f8")) _showOverlay = !_showOverlay;
            if (Input.GetKeyDown("f9")) Diagnostics.Write();
            if (Input.GetKeyDown("f10"))
            {
                if (Plugin.Bridge != null && Plugin.Bridge.Connected)
                {
                    Plugin.Bridge.Send(new Dictionary<string, object> { { "cmd", "Sync" } });
                    AddMessage("Resync requested");
                }
            }
            if (Input.GetKeyDown("f11"))
            {
                Plugin.BypassDayBlock = !Plugin.BypassDayBlock;
                AddMessage(Plugin.BypassDayBlock
                    ? "Day progression no longer waits for abnormality items (F11 to undo)"
                    : "Day progression waits for abnormality items again");
            }
        }

        private void OnApplicationQuit()
        {
            Plugin.Session.Save();
            if (Plugin.Bridge != null) Plugin.Bridge.Stop();
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;
            try
            {
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 13,
                        wordWrap = true,
                        richText = true
                    };
                }
                string text = StatusText();
                float width = 430f;
                float height = 26f + 17f * CountLines(text);
                GUI.Box(new Rect(Screen.width - width - 10f, 40f, width, height), text, _style);
            }
            catch (Exception e)
            {
                Reflect.ReportOnce("overlay", "Overlay failed: " + e.Message);
            }
        }

        private static int CountLines(string text)
        {
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            return lines;
        }

        private string StatusText()
        {
            Session session = Plugin.Session;
            Bridge bridge = Plugin.Bridge;
            var builder = new StringBuilder();
            builder.Append("<b>Archipelago</b> (F8 hide, F9 diagnostics)\n");
            if (bridge == null || !bridge.Connected)
            {
                builder.Append("<color=#ff8080>Client not running</color> - start the Lobotomy Corporation Client ")
                    .Append("(looking for it on 127.0.0.1:").Append(Plugin.Port).Append(")\n");
            }
            else if (!session.ServerConnected)
            {
                builder.Append("<color=#ffd080>Client running, not connected to a server</color>\n");
            }
            else
            {
                builder.Append("<color=#80ff80>Connected</color> as ").Append(session.SlotName).Append('\n');
            }
            if (session.HasSlot)
            {
                builder.Append("Items: ").Append(session.Items.Count)
                    .Append("   Checks: ").Append(session.LocalChecks.Count)
                    .Append("   Goal: Day ").Append(session.GoalDay);
                if (session.RequiredSuppressions > 0)
                {
                    builder.Append(" + ").Append(session.RequiredSuppressions).Append(" suppressions");
                }
                builder.Append('\n');
                if (GameApi.InFacility)
                {
                    int dayIndex = GameApi.DayIndex;
                    int available = Extraction.AvailablePool(dayIndex).Count;
                    builder.Append("Abnormalities ready for extraction: ").Append(available).Append('\n');
                    if (session.PendingTraps.Count > 0)
                    {
                        builder.Append("Traps waiting: ").Append(session.PendingTraps.Count).Append('\n');
                    }
                }
                if (Plugin.BypassDayBlock) builder.Append("<color=#ffd080>Day block bypassed (F11)</color>\n");
                foreach (string warning in _slotWarnings)
                {
                    builder.Append("<color=#ff8080>").Append(warning).Append("</color>\n");
                }
            }
            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<float, string> message in _messages)
            {
                if (now - message.Key > 20f) continue;
                builder.Append("• ").Append(message.Value).Append('\n');
            }
            return builder.ToString().TrimEnd('\n');
        }
    }
}
