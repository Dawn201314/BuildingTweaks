using UnityEngine;

namespace InstantBuild
{
    internal class ConstructionGhostExtended : ConstructionGhost
    {
        protected override void Update()
        {
            if (InstantBuild.FinishBlueprintsEnabled && Input.GetKeyDown(InstantBuild.ModKeybindingId_Finish) && this.GetState() == GhostState.Building && ((P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) || (InstantBuild.PermissionGranted && !InstantBuild.PermissionDenied)))
            {
                Player p = Player.Get();
                if (p != null && InstantBuild.IsWithinRadius(p.GetWorldPosition(), this.transform.position))
                {
                    bool instantBuildBkp = Cheats.m_InstantBuild;
                    if (!this.ReplIsOwner())
                        this.ReplRequestOwnership(false);
                    this.ReplSetDirty();
                    this.ReplSendAsap();
                    Cheats.m_InstantBuild = true;
                    this.SetState(GhostState.Building);
                    this.ReplSetDirty();
                    this.ReplSendAsap();
                    if (this.GetState() != GhostState.Ready)
                    {
                        this.SetState(GhostState.Building);
                        this.ReplSetDirty();
                        this.ReplSendAsap();
                    }
                    Cheats.m_InstantBuild = instantBuildBkp;
#if VERBOSE
                    ModAPI.Log.Write($"[{InstantBuild.ModName}:ConstructionGhostExtended.Update] Blueprint \"{this.GetName()}\" was built instantly at [{this.transform.position.x.ToString("F1")};{this.transform.position.y.ToString("F1")};{this.transform.position.z.ToString("F1")}].");
#endif
                }
            }
            base.Update();
        }
    }
}
