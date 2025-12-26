# AudioKit 使用手册

## 1. 设计理念 (Why)
**AudioKit** 是一个高性能音频管理系统，解决了 Unity 原生 AudioSource 管理混乱、并发过多导致爆音的问题。

### 核心特性
*   **BGM 管理**：支持自动淡入淡出 (`CrossFade`)。
*   **SFX 对象池**：内置 AudioSource 对象池，避免频繁创建销毁。
*   **优先级剔除**：限制最大并发数（默认 64），当池满时，高优先级音效 (`Critical`) 会自动挤掉低优先级音效 (`Low`)。

---

## 2. 快速上手 (Quick Start)

### 2.1 播放音乐 (BGM)
```csharp
// 播放 (自动淡入，默认 0.5s)
AudioKit.PlayMusic("BGM/Battle_Theme");

// 停止
AudioKit.StopMusic();
```

### 2.2 播放音效 (SFX)
```csharp
// 2D 音效 (UI)
AudioKit.PlaySound("SFX/Button_Click");

// 3D 音效 (指定位置)
AudioKit.PlaySound3D("SFX/Explosion", transform.position);

// 3D 音效 (跟随物体)
AudioKit.PlaySound3D("SFX/Footstep", transform);
```

### 2.3 设置
```csharp
AudioKit.MusicVolume = 0.5f;
AudioKit.SoundOn = false; // 静音 SFX
```

---

## 3. 优先级系统 (Priority)
在 `AudioManager` 内部，音效分为 4 个等级：
1.  `Low`: 环境音、脚步声。
2.  `Normal`: 普通攻击、UI。
3.  `High`: 技能、受击。
4.  `Critical`: 剧情对白、Boss 技能。

当同时播放的音效超过 64 个时，系统会优先停止 `Low` 级别的音效来腾出空位。