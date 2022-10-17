using UnityEngine;

namespace InstantBuild
{
    public class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject("__InstantBuildMod__").AddComponent<InstantBuild>();
        }
    }
}
