using System;

namespace InstantBuild
{
    public class P2PSessionExtended : P2PSession
    {
        public override void OnConnected(P2PConnection conn)
        {
            base.OnConnected(conn);
            try
            {
                if (!(this.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) && conn != null && conn.m_Peer == this.GetSessionMaster(false))
                {
                    InstantBuild.WaitAMinBeforeFirstRequest = DateTime.Now.Ticks / 10000000L;
#if VERBOSE
					ModAPI.Log.Write($"[{InstantBuild.ModName}:P2PSessionExtended.OnConnected] Setting initial instant build state from P2PNetworkManagerExtended.OnConnected.");
#endif
                    InstantBuild.SetInstantBuildInitialState();
                }
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write($"[{InstantBuild.ModName}:P2PSessionExtended.OnConnected] Exception caught on connection: [{ex.ToString()}]");
            }
        }

        public override void OnDisconnected(P2PConnection conn)
        {
            try
            {
                if (!(this.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster()) && conn != null && conn.m_Peer == this.GetSessionMaster(false))
                    InstantBuild.RestorePermissionStateToOrig();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write($"[{InstantBuild.ModName}:P2PSessionExtended.OnDisconnected] Exception caught on disconnection: [{ex.ToString()}]");
            }
            base.OnDisconnected(conn);
        }

        public override void SendTextChatMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && message.StartsWith(InstantBuild.PermissionRequestBegin, StringComparison.InvariantCulture) && message.IndexOf(InstantBuild.PermissionRequestEnd, StringComparison.InvariantCulture) > 0)
                return;
            base.SendTextChatMessage(message);
        }
    }
}
