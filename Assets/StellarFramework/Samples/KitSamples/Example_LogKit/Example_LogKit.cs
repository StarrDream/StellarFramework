using System;
using System.Threading;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// LogKit 与 PerformanceKit 标准使用示例
    /// 演示日志分级输出、性能耗时测量与内存监控的标准调用方式
    /// </summary>
    public class Example_LogKit : MonoBehaviour
    {
        private void Start()
        {
            // 1. 基础日志输出 (支持常规、警告、错误)
            LogKit.Log("[Example_LogKit] 模块初始化完成，当前状态: 正常");
            LogKit.LogWarning("[Example_LogKit] 检测到配置缺失，已采用默认回退方案");

            // 2. 性能耗时测量 (仅在 Editor 或 Development Build 生效，Release 零开销)
            PerformanceUtil.MeasureExecutionTime(() =>
            {
                // 模拟复杂的同步初始化逻辑或高强度计算
                SimulateHeavyWorkload();
            }, "SimulateHeavyWorkload");

            // 3. 内存快照打印
            PerformanceUtil.LogMemoryUsage();
        }

        private void Update()
        {
            // 模拟业务输入触发
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ExecuteBusinessLogic(null);
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                // 演示强制 GC 控制 (实机业务中应严格限制调用时机，如场景切换黑屏时)
                PerformanceUtil.ForceGarbageCollection();
                PerformanceUtil.LogMemoryUsage();
            }
        }

        /// <summary>
        /// 模拟耗时操作
        /// </summary>
        private void SimulateHeavyWorkload()
        {
            Thread.Sleep(30); // 模拟 30ms 的主线程阻塞
            LogKit.Log("[Example_LogKit] 耗时操作执行完毕");
        }

        /// <summary>
        /// 演示防御性编程与精准报错规范
        /// </summary>
        private void ExecuteBusinessLogic(GameObject targetEntity)
        {
            // 规范：前置拦截，拒绝深层嵌套与 Try-Catch
            if (targetEntity == null)
            {
                // 规范：精准报错，必须包含类名、触发对象、关键变量状态
                LogKit.LogError($"[Example_LogKit] 业务逻辑执行中断: 传入的 targetEntity 为空，当前帧: {Time.frameCount}");
                return;
            }

            LogKit.Log($"[Example_LogKit] 业务逻辑执行成功，目标对象: {targetEntity.name}");
        }
    }
}