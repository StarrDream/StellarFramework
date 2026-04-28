using UnityEngine;
using StellarFramework.Bindable;

namespace StellarFramework.Examples
{
    /// <summary>
    /// BindableKit 综合使用示例
    /// 演示 0GC 数据绑定的标准工作流
    /// </summary>
    public class Example_BindableKit : MonoBehaviour
    {
        // 1. 基础属性绑定
        public BindableProperty<int> PlayerHP = new BindableProperty<int>(100);

        // 2. 列表绑定
        public BindableList<string> Inventory = new BindableList<string>();

        // 3. 字典绑定
        public BindableDictionary<int, string> QuestStates = new BindableDictionary<int, string>();

        private void Start()
        {
            // 规范：注册监听后，必须调用 UnRegisterWhenGameObjectDestroyed 绑定生命周期防泄漏
            PlayerHP.RegisterWithInitValue(OnHpChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            Inventory.Register(OnInventoryChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            QuestStates.Register(OnQuestStateChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q)) PlayerHP.Value -= 10;
            if (Input.GetKeyDown(KeyCode.W)) Inventory.Add("新武器");
            if (Input.GetKeyDown(KeyCode.E)) QuestStates[1001] = "已完成";
        }

        // 规范：使用方法组代替 Lambda 表达式，彻底消除闭包带来的 GC Alloc
        private void OnHpChanged(int hp)
        {
            LogKit.Log($"[Example_BindableKit] 玩家血量变化: {hp}");
        }

        private void OnInventoryChanged(ListEvent<string> e)
        {
            LogKit.Log($"[Example_BindableKit] 背包变化: {e.Type}, 物品: {e.Item}");
        }

        private void OnQuestStateChanged(DictEvent<int, string> e)
        {
            LogKit.Log($"[Example_BindableKit] 任务状态变化: {e.Type}, 任务ID: {e.Key}, 状态: {e.Value}");
        }
    }
}