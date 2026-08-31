namespace StellarFramework
{
    /// <summary>
    /// 面向高规模业务的无 Closure Timer 接收接口。
    /// </summary>
    public interface ITimeEventReceiver
    {
        /// <summary>处理指定时间事件。</summary>
        void OnTimeEvent(int eventId, in TimeTriggerContext context);
    }
}
