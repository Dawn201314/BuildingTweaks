namespace BuildingTweaks
{
    public class HUDTextChatHistoryExtended : HUDTextChatHistory
    {
        protected override void Awake()
        {
            base.Awake();
            P2PSession.Instance.UnregisterHandler(10, new P2PNetworkMessageDelegate(base.OnTextChat));
            P2PSession.Instance.RegisterHandler(10, new P2PNetworkMessageDelegate(BuildingTweaks.TextChatRecv));
            P2PSession.Instance.RegisterHandler(10, new P2PNetworkMessageDelegate(base.OnTextChat));
        }

        protected override void OnDestroy()
        {
            P2PSession.Instance.UnregisterHandler(10, new P2PNetworkMessageDelegate(BuildingTweaks.TextChatRecv));
            base.OnDestroy();
        }
    }
}