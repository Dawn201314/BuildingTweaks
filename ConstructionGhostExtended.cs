using UnityEngine;

namespace BuildingTweaks
{
    internal class ConstructionGhostExtended : ConstructionGhost
    {
        protected override void Update()
        {
            if (BuildingTweaks.FinishBlueprintsEnabled && Input.GetKeyDown(BuildingTweaks.ModKeybindingId_Finish) && this.GetState() == GhostState.Building && ((P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) || (BuildingTweaks.PermissionGranted && !BuildingTweaks.PermissionDenied)))
            {
                Player p = Player.Get();
                if (p != null && BuildingTweaks.IsWithinRadius(p.GetWorldPosition(), this.transform.position))
                {
                    bool instantBuildBkp = Cheats.m_InstantBuild;
                    if (!this.ReplIsOwner())
                        this.ReplRequestOwnership(false);
                    this.ReplSetDirty();
                    this.ReplSendAsap();
#if DEBUG
                    GhostState currGhostState = this.GetState();
                    ModAPI.Log.Write($"DEBUG: PRE GhostState=[{currGhostState.ToString()}][{System.Convert.ToString((int)currGhostState)}]");
#endif
                    Cheats.m_InstantBuild = true;
                    this.SetState(GhostState.Building);
#if DEBUG
                    currGhostState = this.GetState();
                    ModAPI.Log.Write($"DEBUG: POST GhostState=[{currGhostState.ToString()}][{System.Convert.ToString((int)currGhostState)}]");
#endif
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
                    ModAPI.Log.Write($"[{BuildingTweaks.ModName}:ConstructionGhostExtended.Update] Blueprint \"{this.GetName()}\" was built instantly at [{this.transform.position.x.ToString("F1")};{this.transform.position.y.ToString("F1")};{this.transform.position.z.ToString("F1")}].");
#endif
                }
            }
            base.Update();
        }

        public override void UpdateProhibitionType(bool check_is_snapped = true)
        {
            base.UpdateProhibitionType(check_is_snapped);
            if (BuildingTweaks.BuildEverywhereEnabled && ((P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) || (BuildingTweaks.PermissionGranted && !BuildingTweaks.PermissionDenied)))
                this.m_ProhibitionType = ConstructionGhost.ProhibitionType.None;
        }
    }
}
