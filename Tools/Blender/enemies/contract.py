"""TDD 14.1 part contract: exact set of named child parts per FBX (shared by the build and the preview check)."""
REQUIRED_PARTS = {
    "Enemy_Slime": ["Body", "Eyes"],
    "Enemy_Bat": ["Body", "WingL", "WingR", "Eyes"],
    "Enemy_Skeleton": ["Body", "Head", "ArmL", "ArmR", "Weapon"],
    "Enemy_ShieldKnight": ["Body", "Head", "Shield", "Weapon"],
    "Enemy_Mage": ["Body", "Head", "Hat", "Staff", "StaffGem"],
    "Enemy_Healer": ["Stem", "Cap", "Eyes", "Spores"],
    "Enemy_Bomber": ["Body", "Shell", "Fuse", "LegsL", "LegsR"],
    "Enemy_Totem": ["Base", "Face", "Eyes"],
    "Enemy_BoneWall": ["Wall"],
    "Boss_KingSlime": ["Body", "Crown", "Eyes"],
    "Boss_BoneLich": ["Robe", "Skull", "HandL", "HandR", "Staff", "Orb"],
    "Boss_CrystalGolem": ["Body", "Head", "ArmL", "ArmR", "CoreCrystal", "ShieldCrystal"],
}
