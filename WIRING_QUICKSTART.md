# Bayou — wiring quick start (isometric)

Use this as a **fast checklist**. For deep detail, see the **Comprehensive wiring guide** in [README.md](README.md).

Open the Unity project from the **`Bayou/`** folder (the one that contains `Assets`, `ProjectSettings`, `Packages`).

---

## 0. One-time project setup

| Step | Action |
|------|--------|
| 1 | **Edit → Project Settings → Player → Active Input Handling**: *Input System Package* or *Both* |
| 2 | **Tags**: add tag **`Water`** |
| 3 | **Layers** (optional): dedicated `Ground`, `Water`, `Player` — adjust Physics matrix if you do |

---

## 1. Player (5 minutes)

| Step | Action |
|------|--------|
| 1 | Empty **`Player`** at origin |
| 2 | **Rigidbody**: Gravity ✓, Freeze Rotation **X, Z**, Interpolate, Continuous |
| 3 | **CapsuleCollider**: fits character; **not** trigger |
| 4 | Add **`BayouCharacterMotor`** + **`BayouWaterSensor`** |
| 5 | **`viewTransform`**: drag your **isometric Main Camera** (same camera you play with) |

**New Input:** create **Move** = *Value / Vector2* → assign to **`moveAction`** on `BayouCharacterMotor`.

**Old / Both:** leave `moveAction` empty; uses `Horizontal` / `Vertical`.

---

## 2. Water

| Step | Action |
|------|--------|
| 1 | **Water** mesh/plane object |
| 2 | **Collider**: **Is Trigger** ✓ |
| 3 | Tag object **`Water`** |
| 4 | Add **`WaterVolume`** |

---

## 3. Fishing net

| Step | Action |
|------|--------|
| 1 | Prefab **Net**: **Rigidbody** + **Collider** + **`FishingNetProjectile`** |
| 2 | On **Player**: add **`FishingNetCaster`** |
| 3 | **`netPrefab`** → your Net prefab |
| 4 | **`aimTransform`** → **same camera** as `viewTransform` |
| 5 | **`castOrigin`** → empty child (offset in front / up from feet as you like) |
| 6 | Optional **LineRenderer** → **`trajectoryLine`** (world space) |

**New Input:** **Cast** = *Button* → assign to **`castHoldAction`**. Hold = preview, release = throw.

**Legacy:** hold **left mouse** = preview, release = throw (when old input path is active).

---

## 4. Isometric camera (minimal)

| Step | Action |
|------|--------|
| 1 | **Main Camera**: tag **MainCamera**, position above scene, **tilt down** (~35–55°) |
| 2 | Orthographic or perspective — both OK |
| 3 | Follow player with your preferred method (Cinemachine, script, or empty parent that tracks player) — **do not** rig like first-person (camera on head) |

---

## 5. Play test (30 seconds)

| Check | Expected |
|--------|----------|
| Move | WASD / stick follows **screen** directions when `viewTransform` = isometric camera |
| Water | Entering trigger → slower, heavier feel |
| Cast | Hold cast → arc line; release → net spawns |

---

## 6. When something breaks

| Symptom | First fix |
|---------|-----------|
| No water slowdown | `BayouWaterSensor` on **Player**; water has **trigger** + **`Water` tag** (unless `requiredTag` cleared on `WaterVolume`) |
| Wrong move directions | Set **`viewTransform`** to the **active isometric camera** |
| No net / no preview | Assign **`netPrefab`**, **`castHoldAction`** (or use mouse with legacy path), **`LineRenderer`** if you want a visible arc |
| Build from CLI | Close Unity if project is locked; see [README.md](README.md) build section |
