using System;
using StellarFramework;
using StellarFramework.Event;
using StellarFramework.Pool;
using StellarFramework.UI;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 你的游戏入口
/// </summary>
public class GameEntry : MonoBehaviour
{
    private async void Start()
    {
        GameApp.Interface.Init();
    }
}