using UnityEngine;

namespace InstantBuild
{
    internal class ConstructionGhostExtended : ConstructionGhost
    {
        protected override void Update()
        {
            if (InstantBuild.FinishBlueprintsEnabled && Input.GetKeyDown(InstantBuild.ModKeybindingId_Finish) && this.GetState() == GhostState.Building && (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()))
            {
                Player p = Player.Get();
                if (p != null && InstantBuild.IsWithinRadius(p.GetWorldPosition(), this.transform.position))
                {
                    bool instantBuildBkp = Cheats.m_InstantBuild;
                    Cheats.m_InstantBuild = true;
                    this.SetState(GhostState.Building);
                    Cheats.m_InstantBuild = instantBuildBkp;
                    ModAPI.Log.Write("[InstantBuild:ConstructionGhostExtended] Blueprint \"" + this.GetName() + "\" was built instantly at {" + this.transform.position.x.ToString("F1") + ";" + this.transform.position.y.ToString("F1") + ";" + this.transform.position.z.ToString("F1") + "}.");
                }
            }
            base.Update();
        }
    }
}
