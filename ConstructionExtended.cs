namespace BuildingTweaks
{
    class ConstructionExtended : Construction
    {
        public override void SetUpperLevel(bool set, int level)
        {
            base.SetUpperLevel(set, level);
            if (BuildingTweaks.BuildEverywhereEnabled && ((P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) || (BuildingTweaks.PermissionGranted && !BuildingTweaks.PermissionDenied)))
                this.m_Level = 0;
        }
    }
}
