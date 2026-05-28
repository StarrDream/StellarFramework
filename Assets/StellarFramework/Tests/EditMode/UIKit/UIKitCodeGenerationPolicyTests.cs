using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.UIKit
{
    public sealed class UIKitCodeGenerationPolicyTests
    {
        [Test]
        public void GeneratedBindingFieldsStayVisibleInInspector()
        {
            string codeGenPath = Path.Combine(
                Application.dataPath,
                "StellarFramework/Editor/StellarToolsHub/Modules/UIKit/UIKitCodeGen.cs");

            string codeGenSource = File.ReadAllText(codeGenPath);

            Assert.That(codeGenSource, Does.Contain("[Tooltip(\\\"由 StellarFramework UIKit 自动绑定。仅在修复绑定时手动修改。\\\")]"));
            Assert.That(codeGenSource, Does.Contain("[SerializeField] private {typeName} m_{fieldName};"));
            Assert.That(codeGenSource, Does.Not.Contain("[SerializeField] [HideInInspector] private"));
        }

        [Test]
        public void BindingFailuresReportNodeComponentAndFieldDetails()
        {
            string codeGenPath = Path.Combine(
                Application.dataPath,
                "StellarFramework/Editor/StellarToolsHub/Modules/UIKit/UIKitCodeGen.cs");

            string codeGenSource = File.ReadAllText(codeGenPath);

            Assert.That(codeGenSource, Does.Contain("自动绑定失败：找不到节点"));
            Assert.That(codeGenSource, Does.Contain("自动绑定失败：节点缺少目标组件"));
            Assert.That(codeGenSource, Does.Contain("自动绑定失败：找不到序列化字段"));
        }
    }
}
