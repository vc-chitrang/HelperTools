# Utility Extensions — System #27

Pure C# and Unity extension methods. No MonoBehaviour, no prefab, no scene required.
Import the namespace `HelperTools` and all extensions are immediately available.

---

## Structure

```
Utility_Extensions/
├── Scripts/
│   ├── TransformExtensions.cs
│   ├── VectorExtensions.cs
│   ├── StringExtensions.cs
│   ├── ColorExtensions.cs
│   ├── GameObjectExtensions.cs
│   ├── NumberExtensions.cs
│   ├── CollectionExtensions.cs
│   ├── RectTransformExtensions.cs
│   └── CoroutineExtensions.cs
└── README.md
```

---

## Usage

```csharp
using HelperTools;
```

---

## TransformExtensions

```csharp
transform.ResetLocal();                   // zero position, identity rotation, one scale
transform.SetX(5f);                       // world X only
transform.SetLocalY(2f);                  // local Y only
transform.SetParentAndReset(parent);      // reparent + reset local
transform.DestroyChildren();              // destroy all direct children
transform.GetOrAddComponent<Rigidbody>();
transform.LookAtFlat(targetPos);          // XZ-plane look-at
```

---

## VectorExtensions

```csharp
new Vector3(1, 2, 3).WithY(0f);          // (1, 0, 3)
new Vector2(4, 5).ToVector3(z: 1f);      // (4, 5, 1)
new Vector2(3, 4).ToXZVector3();          // (3, 0, 4) — flat ground plane
someVec.Abs();
someVec.Clamp(0f, 1f);
someVec.IsWithinSqr(other, 25f);          // faster than distance check
VectorExtensions.RandomXZ();              // normalized random direction on XZ
```

---

## StringExtensions

```csharp
"helloWorld".ToTitleCase()      // "Hello World"
"Hello World".ToSnakeCase()     // "hello_world"
"hello_world".ToCamelCase()     // "helloWorld"
"A long sentence".Truncate(10)  // "A long se…"
"abc".Repeat(3)                 // "abcabcabc"
"42".ToInt()                    // 42
"3.14".ToFloat()                // 3.14f
"test@example.com".IsValidEmail() // true
"Score".Bold().Colorize(Color.yellow)
```

---

## ColorExtensions

```csharp
color.WithA(0.5f);
color.Lighten(0.3f);
color.Darken(0.2f);
color.Complement();
color.ToHex();                  // "FF8844"
ColorExtensions.FromHex("FF8844");
color.IsDark();                 // luminance < 0.5
color.ApproximatelyEquals(other, 0.01f);
```

---

## GameObjectExtensions

```csharp
go.GetOrAddComponent<Rigidbody>();
go.SetLayerRecursive("UI");
go.SetVisible(false);            // toggles all Renderers
go.IsInScene();                  // false for prefab assets
go.IsChildOf(parentGO);
go.SafeDestroy();                // Destroy in play, DestroyImmediate in edit
```

---

## NumberExtensions

```csharp
0.7f.Clamp01();
someFloat.Remap(0f, 100f, 0f, 1f);
someFloat.LerpTo(target, 0.1f);
someFloat.ToTimeString();        // "1:23"
someFloat.ToTimeStringLong();    // "1:02:03" if >= 1h
angle.WrapAngle();               // [0, 360)
90f.ToRadians();
3.14f.ToDegrees();
someInt.IsEven();  someInt.IsOdd();
someInt.InRange(0, 10);
```

---

## CollectionExtensions

```csharp
list.IsNullOrEmpty();
list.RandomElement();
list.RandomElementOrDefault();
list.Shuffle();                  // Fisher-Yates in-place
list.Swap(2, 5);
list.GetClamped(index);          // clamps to valid range
list.GetWrapped(index);          // wraps around
list.AddUnique(item);            // no duplicates
list.Pop();                      // remove + return last
list.Fill(10, i => new Enemy(i));
dict.GetOrAdd(key, () => new List<int>());
```

---

## RectTransformExtensions

```csharp
rt.SetWidth(200f);
rt.SetHeight(100f);
rt.SetSize(200f, 100f);
rt.SetAnchoredX(50f);
rt.StretchToParent();            // fill parent
rt.CenterWithSize(new Vector2(400, 200));
var corners = rt.GetWorldCorners();
var center  = rt.GetWorldCenter();
rt.ContainsScreenPoint(Input.mousePosition, Camera.main);
rt.ScreenToLocalPoint(Input.mousePosition, Camera.main, out Vector2 local);
```

---

## CoroutineExtensions

```csharp
// All return Coroutine so you can StopCoroutine if needed

this.DelayedCall(2f, () => Debug.Log("2s later"));
this.DelayedCallRealtime(1f, () => Debug.Log("1 real second"));
this.RepeatCall(0.5f, Tick, times: 5);        // 5 times, every 0.5 s
this.RepeatCall(1f, HeartBeat);               // indefinitely
this.ExecuteOverTime(1f, t => bar.fillAmount = t, () => Done());
this.AfterYield(new WaitForEndOfFrame(), Callback);
this.WaitUntil(() => IsReady, StartGame);
```

---

## Notes

- All methods live in `namespace HelperTools` — a single `using HelperTools;` unlocks everything.
- No MonoBehaviour subclasses, no assets, no dependencies beyond Unity's runtime assemblies.
- Safe to use in any Unity version that supports C# 8+ (Unity 2020.3+).
