using UnityEngine;

namespace BuildingTweaks
{
    public class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject("__InstantBuildMod__").AddComponent<BuildingTweaks>();
        }
    }
}
