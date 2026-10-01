// A tiny simulation of the parts of Lobotomy Corporation's Assembly-CSharp that the Archipelago mod touches.
// It only exists to exercise the mod outside the game (see ModHarness); member names mirror the real game.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public enum RiskLevel { ZAYIN, TETH, HE, WAW, ALEPH }
public enum SefiraEnum { MALKUT, YESOD, HOD, NETZACH, TIPERERTH1, TIPERERTH2, GEBURAH, CHESED, BINAH, CHOKHMAH, KETHER, DAAT, DUMMY }
public enum GameState { PLAYING, PAUSE, STOP }
public enum ConditionType { DESTROY_CORE, MAKE_EQUIPMENT, WORK }
public enum OrdealLevel { DAWN, NOON, DUSK, MIDNIGHT }
public enum OverloadType { DEFAULT, MELTDOWN }

public static class FakeLog
{
    public static readonly List<string> Events = new List<string>();
    public static void Add(string text) { Events.Add(text); }
}

public class Notice
{
    public static Notice instance = new Notice();
    public readonly List<string> SystemLog = new List<string>();
    public void Send(string notice, params object[] param)
    {
        if (notice == "AddSystemLog") SystemLog.Add(param.Length > 0 ? param[0] as string : "");
        FakeLog.Add("notice:" + notice);
    }
}

public static class NoticeName
{
    public static string UpdateResearchItem = "UpdateResearchItem";
    public static string MakeName(string name, params string[] args) { return name + "_" + string.Join("_", args); }
}

public class PlayerModel
{
    public static PlayerModel instance = new PlayerModel();
    public int day;
    public readonly HashSet<long> waiting = new HashSet<long>();
    public int GetDay() { return day; }
    public bool IsWaitingCreature(long id) { return waiting.Contains(id); }
}

public class GameManager
{
    public static GameManager currentGameManager;
    public GameState state = GameState.STOP;
    [MethodImpl(MethodImplOptions.NoInlining)] public void StartGame() { state = GameState.PLAYING; FakeLog.Add("StartGame"); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void EndGame() { state = GameState.STOP; FakeLog.Add("EndGame"); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void ClearAction() { FakeLog.Add("ClearAction day " + PlayerModel.instance.day); }
}

public class StageTypeInfo
{
    public static StageTypeInfo instnace = new StageTypeInfo();
    public float GetEnergyNeed(int day) { return 100f + 50f * day; }
}

// ----------------------------------------------------------------------------------------------------------------
// Equipment
// ----------------------------------------------------------------------------------------------------------------
public class EquipmentTypeInfo
{
    public enum EquipmentType { WEAPON, ARMOR, SPECIAL }
    public int id;
    public EquipmentType type;
    public RiskLevel Grade;
}

public class EquipmentModel
{
    public EquipmentTypeInfo metaInfo;
}

public class InventoryModel
{
    public static InventoryModel Instance = new InventoryModel();
    public List<EquipmentModel> equipList = new List<EquipmentModel>();

    public EquipmentModel CreateEquipmentForcely(int id)
    {
        EquipmentTypeInfo info = CreatureTypeList.instance.FindEquipment(id) ??
                                 new EquipmentTypeInfo { id = id, type = EquipmentTypeInfo.EquipmentType.WEAPON };
        var model = new EquipmentModel { metaInfo = info };
        equipList.Add(model);
        return model;
    }

    public EquipmentModel CreateEquipment(int id) { return CreateEquipmentForcely(id); }
    public void RemoveEquipment(EquipmentModel model) { equipList.Remove(model); }
}

// ----------------------------------------------------------------------------------------------------------------
// Abnormalities
// ----------------------------------------------------------------------------------------------------------------
public class CreatureEquipmentMakeInfo
{
    public EquipmentTypeInfo equipTypeInfo;
    public int level;
    public int cost;
    public int GetCostAfterUpgrade() { return cost; }
}

public class CreatureTypeInfo
{
    public long id;
    public string name;
    public RiskLevel riskLevel;
    public List<CreatureEquipmentMakeInfo> equipMakeInfos = new List<CreatureEquipmentMakeInfo>();
    public RiskLevel GetRiskLevel() { return riskLevel; }
}

public class CreatureTypeList
{
    public static CreatureTypeList instance = new CreatureTypeList();
    public readonly Dictionary<long, CreatureTypeInfo> data = new Dictionary<long, CreatureTypeInfo>();

    public CreatureTypeInfo GetData(long id)
    {
        CreatureTypeInfo info;
        if (data.TryGetValue(id, out info)) return info;
        info = new CreatureTypeInfo { id = id, name = "Abnormality " + id, riskLevel = RiskLevel.TETH };
        if (id < 300000)
        {
            int suffix = (int)(id - 100000);
            info.equipMakeInfos.Add(new CreatureEquipmentMakeInfo
            {
                equipTypeInfo = new EquipmentTypeInfo { id = 200000 + suffix, type = EquipmentTypeInfo.EquipmentType.WEAPON, Grade = RiskLevel.TETH },
                level = 2, cost = 20
            });
            if (id != 100006) // Singing Machine has no suit
            {
                info.equipMakeInfos.Add(new CreatureEquipmentMakeInfo
                {
                    equipTypeInfo = new EquipmentTypeInfo { id = 300000 + suffix, type = EquipmentTypeInfo.EquipmentType.ARMOR, Grade = RiskLevel.TETH },
                    level = 3, cost = 30
                });
            }
        }
        data[id] = info;
        return info;
    }

    public EquipmentTypeInfo FindEquipment(int id)
    {
        foreach (CreatureTypeInfo info in data.Values)
            foreach (CreatureEquipmentMakeInfo make in info.equipMakeInfos)
                if (make.equipTypeInfo.id == id) return make.equipTypeInfo;
        return null;
    }
}

public class CreatureObserveInfoModel
{
    public int cubeNum;
    public void Transaction(int amount) { cubeNum += amount; }
}

public class CreatureModel
{
    private static long _nextInstance = 1;
    public long instanceId = _nextInstance++;
    public long metadataId;
    public CreatureTypeInfo metaInfo;
    public CreatureObserveInfoModel observeInfo = new CreatureObserveInfoModel();
    public int observationLevel;
    public int qliphothCounter = 2;

    public int GetObservationLevel() { return observationLevel; }
    public void SetQliphothCounter(int value) { qliphothCounter = value; FakeLog.Add("qliphoth " + metadataId + " = " + value); }
}

public class CreatureManager
{
    public static CreatureManager instance = new CreatureManager();
    public readonly List<CreatureModel> creatures = new List<CreatureModel>();
    public CreatureModel[] GetCreatureList() { return creatures.ToArray(); }

    public CreatureModel Add(long id)
    {
        var model = new CreatureModel { metadataId = id, metaInfo = CreatureTypeList.instance.GetData(id) };
        creatures.Add(model);
        return model;
    }
}

namespace CreatureGenerate
{
    public class CreatureGenerateModel
    {
        public int day;
        public List<long> creature = new List<long>();

        // Vanilla: three random doors.
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetCreature()
        {
            creature.Clear();
            if ((PlayerModel.instance.day + 1) % 5 == 0) return;
            creature.Add(100001);
            creature.Add(100002);
            creature.Add(100003);
        }
    }
}

public class CreatureSelectUI
{
    public List<long> CurrentCreatures = new List<long>();

    [MethodImpl(MethodImplOptions.NoInlining)] public void GetCreatureList(bool setEmpty)
    {
        var model = new CreatureGenerate.CreatureGenerateModel();
        model.SetCreature();
        CurrentCreatures.Clear();
        CurrentCreatures.AddRange(model.creature);
    }
}

public class GameSceneController
{
    public bool advanced;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnClickNextDay() { advanced = true; }
}

public class AudioClipPlayer
{
    public void OnPlayInList(int index) { FakeLog.Add("sound " + index); }
}

public class CreatureInfoWindow
{
    public static CreatureInfoWindow CurrentWindow = new CreatureInfoWindow();
    public AudioClipPlayer audioClipPlayer = new AudioClipPlayer();
    public void PurchaseAnim(int cost) { FakeLog.Add("purchase anim " + cost); }
}

public class CreatureInfoEquipmentRoot
{
    public CreatureModel model;
    public CreatureTypeInfo MetaInfo { get { return model.metaInfo; } }
    public CreatureObserveInfoModel ObserveInfo { get { return model.observeInfo; } }
    public int GetObserveLevel() { return model.observationLevel; }
    public void Initialize() { FakeLog.Add("equipment root refresh"); }

    [MethodImpl(MethodImplOptions.NoInlining)] public void OnClickWeapon() { VanillaExtract(EquipmentTypeInfo.EquipmentType.WEAPON); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnClickArmor() { VanillaExtract(EquipmentTypeInfo.EquipmentType.ARMOR); }

    private void VanillaExtract(EquipmentTypeInfo.EquipmentType type)
    {
        CreatureEquipmentMakeInfo info = MetaInfo.equipMakeInfos.FirstOrDefault(i => i.equipTypeInfo.type == type);
        if (info == null || info.level > GetObserveLevel() || info.cost > ObserveInfo.cubeNum) return;
        ObserveInfo.Transaction(-info.cost);
        InventoryModel.Instance.CreateEquipment(info.equipTypeInfo.id);
        FakeLog.Add("vanilla extraction " + info.equipTypeInfo.id);
    }
}

// ----------------------------------------------------------------------------------------------------------------
// Employees and departments
// ----------------------------------------------------------------------------------------------------------------
public class WorkerPrimaryStat
{
    public int hp = 20, mental = 20, work = 20, battle = 20;
}

public class AgentModel
{
    private static long _nextInstance = 1;
    public long instanceId = _nextInstance++;
    public string currentSefira = "0";
    public WorkerPrimaryStat primaryStat = new WorkerPrimaryStat();
    public float hp = 10f, mental = 10f;
    public float maxHp { get { return 50f; } }
    public float maxMental { get { return 50f; } }
    public bool dead, panic;

    public bool IsDead() { return dead; }
    public bool IsPanic() { return panic; }
    public void Panic() { panic = true; }
    public void Die() { dead = true; OnDie(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnDie() { FakeLog.Add("agent died " + instanceId); }
    public List<EquipmentModel> GetAllGifts() { return new List<EquipmentModel>(); }

    [MethodImpl(MethodImplOptions.NoInlining)] public void SetCurrentSefira(string sefira)
    {
        foreach (Sefira s in SefiraManager.instance.sefiras) s.agentList.Remove(this);
        currentSefira = sefira;
        Sefira target = SefiraManager.instance.GetSefira(sefira);
        if (target != null) target.agentList.Add(this);
    }
}

public class AgentManager
{
    public static AgentManager instance = new AgentManager();
    public readonly List<AgentModel> agents = new List<AgentModel>();
    public List<AgentModel> GetAgentList() { return agents; }
}

public class Sefira
{
    public SefiraEnum sefiraEnum;
    public string indexString;
    public string name;
    public List<AgentModel> agentList = new List<AgentModel>();
}

public class SefiraManager
{
    public static SefiraManager instance = new SefiraManager();
    public readonly List<Sefira> sefiras = new List<Sefira>
    {
        new Sefira { sefiraEnum = SefiraEnum.MALKUT, indexString = "1", name = "Control" },
        new Sefira { sefiraEnum = SefiraEnum.YESOD, indexString = "4", name = "Information" },
    };

    public Sefira GetSefira(SefiraEnum sefira) { return sefiras.FirstOrDefault(s => s.sefiraEnum == sefira); }
    public Sefira GetSefira(string index) { return sefiras.FirstOrDefault(s => s.indexString == index); }
    public List<Sefira> GetActivatedSefiras() { return sefiras; }
}

public class MoneyModel
{
    public static MoneyModel instance = new MoneyModel();
    public int money;
    public void Add(int amount) { money += amount; }
}

public class EnergyModel
{
    public static EnergyModel instance = new EnergyModel();
    public float energy;
    public float GetEnergy() { return energy; }
    public void AddEnergy(float amount) { energy += amount; }
    public void SubEnergy(float amount) { energy -= amount; }
}

// ----------------------------------------------------------------------------------------------------------------
// Research and missions
// ----------------------------------------------------------------------------------------------------------------
public class ResearchDesc { public string name; }

public class ResearchItemTypeInfo
{
    public int id;
    public string name;
    public ResearchDesc GetDesc() { return new ResearchDesc { name = name }; }
}

public class ResearchItemModel
{
    public ResearchItemTypeInfo info;
    public int curLevel;
}

public class ResearchDataModel
{
    public static ResearchDataModel instance = new ResearchDataModel();
    public readonly Dictionary<string, List<ResearchItemModel>> bySefira = new Dictionary<string, List<ResearchItemModel>>
    {
        { "1", new List<ResearchItemModel> {
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 1, name = "TT2 Protocol" } },
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 2, name = "Joint Command" } },
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 101, name = "Assembly Summons" } } } },
        { "5", new List<ResearchItemModel> {
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 401, name = "Physical Intervention Shield" } },
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 402, name = "Trauma Shield" } },
            new ResearchItemModel { info = new ResearchItemTypeInfo { id = 403, name = "Erosion Shield" } } } },
    };

    public List<ResearchItemModel> GetModelBySefira(string index)
    {
        List<ResearchItemModel> list;
        return bySefira.TryGetValue(index, out list) ? list : new List<ResearchItemModel>();
    }

    public List<ResearchItemModel> GetUpgradedResearchListBySefira(SefiraEnum sefira)
    {
        return bySefira.Values.SelectMany(l => l).Where(r => r.curLevel > 0).ToList();
    }

    public bool UpgradeResearch(int id, bool force)
    {
        foreach (ResearchItemModel item in bySefira.Values.SelectMany(l => l))
        {
            if (item.info.id != id || item.curLevel > 0) continue;
            item.curLevel = 1;
            FakeLog.Add("research " + id);
            return true;
        }
        return false;
    }
}

public class MissionTypeInfo { public int id; public SefiraEnum sefira; }
public class SuccessCondition { public ConditionType condition_Type; public int current; }

public class Mission
{
    public MissionTypeInfo metaInfo;
    public SuccessCondition successCondition;
    public void CheckSuccess() { FakeLog.Add("mission check " + metaInfo.id); }
}

public class MissionManager
{
    public static MissionManager instance = new MissionManager();
    public readonly List<Mission> cleared = new List<Mission>();
    public readonly List<Mission> inProgress = new List<Mission>();
    public readonly HashSet<SefiraEnum> bossesDone = new HashSet<SefiraEnum>();

    public List<Mission> GetClearedMissions() { return new List<Mission>(cleared); }
    public List<Mission> GetMissionsInProgress() { return inProgress; }
    public bool ExistsFinishedBossMission(SefiraEnum sefira) { return bossesDone.Contains(sefira); }
    public void CloseClearedMission_ExceptBoss(SefiraEnum sefira)
    {
        cleared.RemoveAll(m => m.metaInfo.sefira == sefira && m.successCondition.condition_Type != ConditionType.DESTROY_CORE);
        FakeLog.Add("closed missions " + sefira);
    }
}

public class DeployUI
{
    public bool researchWindowOpened;
    [MethodImpl(MethodImplOptions.NoInlining)] public void CheckResearchAvailable() { researchWindowOpened = true; }
}

// ----------------------------------------------------------------------------------------------------------------
// Ordeals, overload, misc
// ----------------------------------------------------------------------------------------------------------------
public class OrdealBase { }
public class MachineDawnOrdeal : OrdealBase { }
public class BugDawnOrdeal : OrdealBase { }
public class MachineMidnightOrdeal : OrdealBase { }
public class FixerOrdeal : OrdealBase { public FixerOrdeal(OrdealLevel level) { } }

public class OrdealManager
{
    public static OrdealManager instance = new OrdealManager();
    public void ActivateOrdeal(OrdealBase ordeal, bool flag) { FakeLog.Add("ordeal " + ordeal.GetType().Name); }
}

public class CreatureOverloadManager
{
    public static CreatureOverloadManager instance = new CreatureOverloadManager();
    public int gauge;
    public void AddOverloadGague() { gauge++; }
}

public class KetherLastBossBase { [MethodImpl(MethodImplOptions.NoInlining)] public void ToCredit() { FakeLog.Add("credits"); } }
public class DontTouchMe { [MethodImpl(MethodImplOptions.NoInlining)] public void ExitGame() { FakeLog.Add("exit game"); } }
public class EscapeUI { [MethodImpl(MethodImplOptions.NoInlining)] public void RestartAtCheckpoint() { FakeLog.Add("restart"); } }
public class NewTitleScript { [MethodImpl(MethodImplOptions.NoInlining)] public void Update() { } }
public class UnitMouseEventManager { [MethodImpl(MethodImplOptions.NoInlining)] public void Update() { } }
