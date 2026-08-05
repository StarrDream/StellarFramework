using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// }}

	public void RefMethods()
	{
		// 补充热更代码可能用到的常用泛型实例化，防止 IL2CPP AOT 侧缺少泛型补充导致 ExecutionEngineException。
		// 仅使用无参构造的泛型容器与可空值类型，确保零编译风险；这些手动引用会与 HybridCLR Generate 生成的引用合并保留。
		new List<int>();
		new List<string>();
		new List<float>();
		new List<bool>();
		new List<UnityEngine.GameObject>();
		new List<UnityEngine.Transform>();
		new List<object>();
		new Dictionary<string, int>();
		new Dictionary<string, string>();
		new Dictionary<string, float>();
		new Dictionary<System.Type, object>();
		new Dictionary<string, List<object>>();
		new Dictionary<int, string>();
		new HashSet<string>();
		new HashSet<int>();
		new Queue<object>();
		new Stack<object>();
		new System.Nullable<int>();
		new System.Nullable<float>();
		new System.Nullable<bool>();
		new System.Nullable<long>();
		new System.Nullable<UnityEngine.Vector3>();
	}
}