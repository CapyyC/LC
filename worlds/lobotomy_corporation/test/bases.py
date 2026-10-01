from test.bases import WorldTestBase

from .. import LobotomyCorporationWorld


class LobotomyCorporationTestBase(WorldTestBase):
    game = "Lobotomy Corporation"
    world: LobotomyCorporationWorld
