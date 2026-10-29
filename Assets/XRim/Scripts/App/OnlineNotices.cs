using XRim.Networking.Protocol;

namespace XRim.App
{
    /// <summary>
    /// What the match HUD's banner says about connections during an online match (GDD §3; designer 2026-10-25, Q4): this device's own link
    /// dropping (it rejoins by itself), and the opponent dropping, the server holding the next turn for them (counting down), going on
    /// without them, or their return (the banner clears). This device's own trouble comes first. Pure: the times are the caller's clock.
    /// </summary>
    public sealed class OnlineNotices
    {
        private bool _ownLinkDown;
        private ConnectionNoticeMessage _opponent;
        private double _opponentSince;

        public void OwnConnection(bool connected) => _ownLinkDown = !connected;

        public void Opponent(ConnectionNoticeMessage notice, double nowSeconds)
        {
            _opponent = notice != null && notice.Status != ConnectionStatus.Reconnected ? notice : null;
            _opponentSince = nowSeconds;
        }

        public void Clear()
        {
            _ownLinkDown = false;
            _opponent = null;
        }

        /// <summary>The banner's headline now; empty when there is nothing to say.</summary>
        public string Title(double nowSeconds)
        {
            if (_ownLinkDown) return MatchLabels.OnlineReconnectingTitle;
            if (_opponent == null) return string.Empty;
            switch (_opponent.Status)
            {
                case ConnectionStatus.Dropped:
                    return WaitLeft(nowSeconds) > 0.0 ? MatchLabels.OpponentReconnectingTitle : MatchLabels.OpponentDisconnectedTitle;
                default:
                    return MatchLabels.OpponentGoneTitle;
            }
        }

        /// <summary>The banner's second line now.</summary>
        public string Detail(double nowSeconds)
        {
            if (_ownLinkDown) return MatchLabels.OnlineReconnectingDetail;
            if (_opponent == null) return string.Empty;
            switch (_opponent.Status)
            {
                case ConnectionStatus.Dropped:
                    double left = WaitLeft(nowSeconds);
                    return left > 0.0 ? MatchLabels.OpponentReconnectingDetail(left) : MatchLabels.OpponentDisconnectedDetail;
                default:
                    return MatchLabels.OpponentGoneDetail;
            }
        }

        private double WaitLeft(double nowSeconds) => _opponent.WaitSecondsLeft - (nowSeconds - _opponentSince);
    }
}
