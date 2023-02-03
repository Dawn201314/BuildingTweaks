using System;
using UnityEngine;

namespace BuildingTweaks
{
    public class HUDTextChatExtended : HUDTextChat
    {
        public override void ConstantUpdate()
        {
            base.ConstantUpdate();
            if (BuildingTweaks.DoRequestPermission)
            {
                BuildingTweaks.DoRequestPermission = false;
                if (BuildingTweaks.WaitAMinBeforeFirstRequest > 0L)
                {
                    if (((DateTime.Now.Ticks / 10000000L) - BuildingTweaks.WaitAMinBeforeFirstRequest) > 59L)
                        BuildingTweaks.WaitAMinBeforeFirstRequest = -1L;
                    else
                    {
                        BuildingTweaks.ShowHUDError("You need to wait one minute before doing your first permission request.");
                        return;
                    }
                }
                if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.IsPlayingAlone() || ReplTools.AmIMaster())
                {
                    BuildingTweaks.ShowHUDError("Cannot request permission because you are the host or in singleplayer mode.");
                    return;
                }
                if (HUDNameAnimal.Get().IsActive() || HUDNameAnimal.Get().GetTimeSinceDeactivation() < 0.5f)
                {
                    BuildingTweaks.ShowHUDError("Cannot request permission because you are currently giving a name to an animal.");
                    return;
                }
                if (BuildingTweaks.PermissionGranted)
                {
                    BuildingTweaks.ShowHUDInfo("Host already gave you permission.");
                    return;
                }
                if (BuildingTweaks.PermissionDenied)
                {
                    BuildingTweaks.ShowHUDError("Host has denied permission or did not reply. Please restart the game to ask permission again.");
                    return;
                }
                if (BuildingTweaks.NbPermissionRequests >= 3)
                {
                    BuildingTweaks.ShowHUDError("You've reached the maximum amount of permission requests. Please restart the game to ask permission again.");
                    return;
                }
                if (BuildingTweaks.WaitingPermission)
                {
                    BuildingTweaks.ShowHUDError("You've requested permission less than a minute ago. Please wait one minute to ask again.");
                    return;
                }
                if (BuildingTweaks.OtherWaitingPermission)
                {
                    BuildingTweaks.ShowHUDError("Another player requested permission less than a minute ago. Please wait one minute to ask permission.");
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
                            BuildingTweaks.ShowHUDError("Sending permission request failed (a chat window is active). Please try again.");
                            return;
                        }
                    }
                    P2PNetworkWriter writer = new P2PNetworkWriter();
                    writer.StartMessage(10);
                    writer.Write(BuildingTweaks.PermissionRequestFinal);
                    writer.FinishMessage();
                    P2PSession.Instance.SendWriterToAll(writer, 1);

                    BuildingTweaks.NbPermissionRequests = (BuildingTweaks.NbPermissionRequests + 1);
                    BuildingTweaks.PermissionAskTime = DateTime.Now.Ticks / 10000000L;
                    BuildingTweaks.WaitingPermission = true;
                    if (this.m_History)
                        this.m_History.StoreMessage(BuildingTweaks.PermissionRequestFinal, ReplTools.GetLocalPeer().GetDisplayName(), new Color?(ReplicatedLogicalPlayer.s_LocalLogicalPlayer ? ReplicatedLogicalPlayer.s_LocalLogicalPlayer.GetPlayerColor() : HUDTextChatHistory.NormalColor));
                    BuildingTweaks.ShowHUDInfo("Permission has been requested. Please wait one minute for the host to reply.");
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write($"[{BuildingTweaks.ModName}:HUDTextChatExtended.ConstantUpdate] Exception caught while trying to send permission request: [{ex.ToString()}]");
                }
            }
        }
    }
}
