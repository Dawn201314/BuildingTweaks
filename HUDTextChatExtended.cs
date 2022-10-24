using System;
using UnityEngine;

namespace InstantBuild
{
    public class HUDTextChatExtended : HUDTextChat
    {
        public override void ConstantUpdate()
        {
            base.ConstantUpdate();
            if (InstantBuild.DoRequestPermission)
            {
                InstantBuild.DoRequestPermission = false;
                if (InstantBuild.WaitAMinBeforeFirstRequest > 0L)
                {
                    if (((DateTime.Now.Ticks / 10000000L) - InstantBuild.WaitAMinBeforeFirstRequest) > 59L)
                        InstantBuild.WaitAMinBeforeFirstRequest = -1L;
                    else
                    {
                        InstantBuild.ShowHUDError("You need to wait one minute before doing your first permission request.");
                        return;
                    }
                }
                if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.IsPlayingAlone() || ReplTools.AmIMaster())
                {
                    InstantBuild.ShowHUDError("Cannot request permission because you are the host or in singleplayer mode.");
                    return;
                }
                if (HUDNameAnimal.Get().IsActive() || HUDNameAnimal.Get().GetTimeSinceDeactivation() < 0.5f)
                {
                    InstantBuild.ShowHUDError("Cannot request permission because you are currently giving a name to an animal.");
                    return;
                }
                if (InstantBuild.PermissionGranted)
                {
                    InstantBuild.ShowHUDInfo("Host already gave you permission.");
                    return;
                }
                if (InstantBuild.PermissionDenied)
                {
                    InstantBuild.ShowHUDError("Host has denied permission or did not reply. Please restart the game to ask permission again.");
                    return;
                }
                if (InstantBuild.NbPermissionRequests >= 3)
                {
                    InstantBuild.ShowHUDError("You've reached the maximum amount of permission requests. Please restart the game to ask permission again.");
                    return;
                }
                if (InstantBuild.WaitingPermission)
                {
                    InstantBuild.ShowHUDError("You've requested permission less than a minute ago. Please wait one minute to ask again.");
                    return;
                }
                if (InstantBuild.OtherWaitingPermission)
                {
                    InstantBuild.ShowHUDError("Another player requested permission less than a minute ago. Please wait one minute to ask permission.");
                    return;
                }
                try
                {
                    if (this.m_ShouldBeVisible || InputsManager.Get().m_TextInputActive)
                    {
                        this.m_Field.text = string.Empty;
                        this.m_Field.DeactivateInputField();
                        this.m_Field.text = string.Empty;
                        this.m_ShouldBeVisible = false;
                        InputsManager.Get().m_TextInputActive = false;
                        if (this.m_ShouldBeVisible || InputsManager.Get().m_TextInputActive)
                        {
                            InstantBuild.ShowHUDError("Sending permission request failed (a chat window is active). Please try again.");
                            return;
                        }
                    }
                    P2PNetworkWriter writer = new P2PNetworkWriter();
                    writer.StartMessage(10);
                    writer.Write(InstantBuild.PermissionRequestFinal);
                    writer.FinishMessage();
                    P2PSession.Instance.SendWriterToAll(writer, 1);

                    InstantBuild.NbPermissionRequests = (InstantBuild.NbPermissionRequests + 1);
                    InstantBuild.PermissionAskTime = DateTime.Now.Ticks / 10000000L;
                    InstantBuild.WaitingPermission = true;
                    if (this.m_History)
                        this.m_History.StoreMessage(InstantBuild.PermissionRequestFinal, ReplTools.GetLocalPeer().GetDisplayName(), new Color?(ReplicatedLogicalPlayer.s_LocalLogicalPlayer ? ReplicatedLogicalPlayer.s_LocalLogicalPlayer.GetPlayerColor() : HUDTextChatHistory.NormalColor));
                    InstantBuild.ShowHUDInfo("Permission has been requested. Please wait one minute for the host to reply.");
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write($"[{InstantBuild.ModName}:HUDTextChatExtended.ConstantUpdate] Exception caught while trying to send permission request: [{ex.ToString()}]");
                }
            }
        }
    }
}
