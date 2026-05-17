# Bayou

Top-down **isometric** bayou fishing (not first-person). Movement and net aim use the **camera’s forward/right projected onto the ground (XZ)** so WASD matches “up/down/left/right” on screen for a tilted camera.

**Other guide:** fast checklist → [WIRING_QUICKSTART.md](WIRING_QUICKSTART.md)

## Comprehensive wiring guide

This project lives under `Bayou/` (open that folder as the Unity project root). Scripts are under `Bayou/Assets/Scripts/`.

### Prerequisites

- **Unity**: Version in `Bayou/ProjectSettings/ProjectVersion.txt` (match with Unity Hub).
- **Input**: Either **Both** or **Input System Package** in **Edit → Project Settings → Player → Active Input Handling**.  
  With the **New Input System** only, assign `InputActionReference` fields below; with **Both**, legacy mouse still works for fishing.
- **Tag**: Create a tag **`Water`** (**Edit → Project Settings → Tags and Layers**) if you use the default water setup.

---

### 1. Player (movement + water sensing)

#### 1.1 Hierarchy

Create a root **`Player`** (or use your model as root).

Recommended hierarchy (camera **not** under the player’s head — isometric rig is usually a **sibling** or managed by a follow script):

```text
IsometricRig (optional parent)
├─ Main Camera                 ← tilted down (~30–55°), offset above the play space
└─ Player
   ├─ Model
   └─ CastOrigin (empty)       ← optional; in front of character on the ground plane
```

#### 1.2 Components on `Player` (root)

| Component | Purpose |
|-----------|---------|
| **Rigidbody** | Use Gravity. Mass ~1–80 as you prefer. **Constraints**: Freeze Rotation **X** and **Z** (Y free for future jump). **Interpolation**: Interpolate. **Collision Detection**: Continuous. |
| **Capsule Collider** | Fits your character height/radius. **Not** a trigger (solid body). |
| **BayouCharacterMotor** | Land movement; slows in water. |
| **BayouWaterSensor** | Detects water **trigger** volumes. |

**Important:** `OnTriggerEnter` on `BayouWaterSensor` only runs if Unity can generate trigger messages. Common setup: **player Rigidbody + player Collider** + **water = Trigger collider**. Do not put `BayouWaterSensor` only on a static object with no Rigidbody—put it on the **moving** character.

#### 1.3 `BayouCharacterMotor` fields

| Field | Wiring |
|-------|--------|
| **View Transform** | Drag your **isometric Main Camera** (or a child pivot that shares the same yaw as the camera). `BayouCharacterMotor` flattens the camera’s forward/right to **XZ** so movement matches the screen. Leave empty only if you want movement relative to the **player’s** forward. |
| **Move Action** (New Input System) | See [Section 3](#3-new-input-system-wiring). Action type: **Value**, **Control Type: Vector 2**. Bind WASD or left stick. |
| **Ground / Water tunables** | Adjust `maxSpeed`, `waterSpeedMultiplier`, etc. after playtesting. |
| **Ground Mask** | Limit to layers you walk on (exclude water layer if you split layers later). |

#### 1.4 `BayouWaterSensor` fields

| Field | Wiring |
|-------|--------|
| **Accept Water Tag Without Component** | Default **true**: any collider tagged `Water` counts, even without `WaterVolume`. Set **false** if you only want volumes that use `WaterVolume`. |

---

### 2. Water volume (slow / heavy movement)

#### 2.1 Water GameObject

1. Create a **`Water`** region: plane, box, or custom mesh.
2. Add a **Collider** (`BoxCollider` / `MeshCollider`). Enable **Is Trigger**.
3. Tag the object **`Water`** (unless you clear `WaterVolume.requiredTag`).
4. Add **`WaterVolume`**.

#### 2.2 `WaterVolume` behavior

| `requiredTag` | Meaning |
|---------------|---------|
| **`Water`** (default) | The **water object’s** GameObject must have tag `Water`. |
| **Empty** | Any trigger with `WaterVolume` is accepted (tag ignored). |

The **player** does not need the `Water` tag; the **water volume** does (when using the default).

---

### 3. New Input System wiring

#### 3.1 Install / enable

- **Window → Package Manager**: **Input System** package installed.
- **Player** settings: **Active Input Handling** = *Input System Package* or *Both*.

#### 3.2 Input Actions asset (recommended)

1. **Create** `InputSystem_Actions.inputactions` (or add to existing).
2. Add an **Action Map** e.g. `Player`.
3. Actions:

| Action name | Type | Bindings (examples) |
|-------------|------|---------------------|
| **Move** | Value, **Vector2** | WASD composite, or Left Stick |
| **Cast** | **Button** | Left mouse button, or gamepad shoulder |

4. **Save** and **Generate C# Class** (optional) or use **references** only.

#### 3.3 Hooking references on components

- **`BayouCharacterMotor` → Move Action**  
  Drag the **Move** action from the asset (or a saved `.inputactions` sub-asset) into **`InputActionReference`**. At runtime the motor reads **Vector2** movement.

- **`FishingNetCaster` → Cast Hold Action**  
  Drag the **Cast** **Button** action. Behavior: **Started** = begin aim + trajectory; **Canceled** = release = throw net.

#### 3.4 `PlayerInput` (optional)

You do **not** have to add **`PlayerInput`** for these scripts—they use **`InputActionReference`** directly. Add **`PlayerInput`** only if you want Unity’s default action map switching / UI focus behavior.

#### 3.5 Legacy fallback

If **Active Input Handling** is **Input Manager (Old)** or **Both**:

- **Move**: `Horizontal` / `Vertical` axes (default Project Settings).
- **Cast**: **Left Mouse Button** hold/release on `FishingNetCaster` (when New Input callbacks are not compiled/active).

---

### 4. Fishing (net + trajectory line)

#### 4.1 Net prefab

Create **`Net`** prefab:

| Component | Notes |
|-----------|--------|
| **Rigidbody** | Non-kinematic before launch; script sets velocity. |
| **Collider** | `Sphere` or `Capsule`; non-trigger for ground hits. |
| **FishingNetProjectile** | Tune **Life Seconds**, **In Water Linear Damping**, **Stick On Impact**. |

Prefab must be assigned to **`FishingNetCaster.netPrefab`**.

#### 4.2 Add `FishingNetCaster` to Player

Usually on the **same root** as the motor (or a child “Hands” object).

| Field | Wiring |
|-------|--------|
| **Cast Origin** | Empty child at **spawn height** (e.g. above feet / waist); net **launches along camera-derived XZ** from `ComputeLaunch`. |
| **Aim Transform** | Same **isometric camera** as `viewTransform` so movement and cast direction stay **consistent** (both use forward flattened to the ground). |
| **Net Prefab** | Your **Net** prefab. |
| **Trajectory Line** | Optional: add **LineRenderer** on Player or world UI—assign here. Use **World Space**, few units width, unlit material if needed. |
| **Cast Hold Action** | New Input: **Button**—see [Section 3](#3-new-input-system-wiring). |
| **Collision Mask** | Layers the trajectory **linecast** should hit (ground, shore—not if you want arc through air only). |

#### 4.3 Controls summary

| Mode | Cast / aim |
|------|------------|
| **New Input** | Hold **Cast** → preview arc; release → spawn net. |
| **Legacy** | Hold **LMB** → preview; release → cast. |

---

### 5. Top-down isometric camera

- Tag **`Main Camera`** as **MainCamera** if `FishingNetCaster` / `Reset()` uses `Camera.main`.
- **Typical isometric setup**
  - **Position**: above and “behind” the play area (world +Z or +X depending on taste).
  - **Rotation**: pitch ~**35–55°** down; yaw sets which compass direction is “up” on the monitor.
  - **Projection**: **Orthographic** is common for crisp iso; **Perspective** works too — both are fine with these scripts.
- **Follow**: use **Cinemachine**, a simple **late-update follow** script, or **parent** the camera to an empty that tracks the player — avoid nesting the camera under the character’s **head** (first-person style).
- **Wiring**: assign the **same** camera transform to **`BayouCharacterMotor.viewTransform`** and **`FishingNetCaster.aimTransform`** so walk direction and throw direction use one consistent screen-aligned basis.

---

### 6. Layers and physics (troubleshooting)

| Issue | Check |
|-------|--------|
| WASD / stick doesn’t match screen directions | Assign **isometric camera** to `viewTransform` on `BayouCharacterMotor` (same as fishing `aimTransform` for consistency). |
| No water slowdown | Player has **Rigidbody** + **Collider**; water has **trigger** collider + **`Water` tag** (or empty `requiredTag` on `WaterVolume`). `BayouWaterSensor` is on the **player**. |
| Triggers ignored | Physics **layer matrix** (**Edit → Project Settings → Physics**) allows collisions between **Player** and **Water** layers. |
| Net falls through ground | Ground has collider; net’s Rigidbody/collider not disabled; not on ignore layer. |
| No trajectory line | Assign **LineRenderer** to `FishingNetCaster`. Check material / width / world space. |
| New Input does nothing | **InputActionReference** assigned; action **enabled**; **Active Input Handling** includes Input System; bindings correct for your device. |

---

### 7. Build scripts (optional)

Command-line builds use **`Bayou.Build.BuildPlayer.BuildFromCommandLine`**. Settings live in **`Assets/Editor/Build/BayouBuildSettings.asset`** (auto-created via **Tools → Bayou → Build → Settings**).

Close Unity before **batchmode** CLI builds if your environment locks the project (`Temp/UnityLockfile`).

From repo root:

```powershell
.\build.ps1 -Target Win64
```

See sections below for paths and overrides.

---

## Build (command line)

This is a Unity project (Unity version is pinned in `Bayou/ProjectSettings/ProjectVersion.txt`).

From the repo root:

```powershell
.\build.ps1 -Target Win64
```

Outputs go to `.\artifacts\<Target>\` and logs to `.\artifacts\logs\`.

### Common options

```powershell
.\build.ps1 -Target Win64 -Development
.\build.ps1 -Target Win64 -Version 0.1.0
.\build.ps1 -Target WebGL -Output .\artifacts\WebGL
```

### If Unity isn't found

The build script tries Unity Hub default install paths like:

- `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`

You can override with:

```powershell
.\build.ps1 -UnityExe 'C:\Path\To\Unity.exe'
```

Or set an environment variable:

```powershell
$env:UNITY_PATH = 'C:\Path\To\Unity.exe'
.\build.ps1
```
