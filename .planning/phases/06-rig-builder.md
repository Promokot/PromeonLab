# Phase 6: RigBuilder — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Select an imported skinned mesh → open RigBuilder panel → click "Build Rig" → bones appear as selectable proxy spheres; select root and end bone → click "Create IK Chain" → a `TwoBoneIKConstraint` is applied.

**Architecture:** `RigDefinition` is a plain C# data class serialized to `rig-{assetId}.json`. `RigRuntime` is a MonoBehaviour that applies the `RigDefinition` to an existing `SkinnedMeshRenderer` using Unity's Animation Rigging package (`Rig`, `RigBuilder`, constraints). `BoneProxy` is a small selectable sphere spawned per bone; clicking it fires `SelectionChangedEvent` with the bone's node ID. `IkSetupWizard` is a simple two-step panel: pick root bone, pick end bone, confirm.

**Tech Stack:** Unity Animation Rigging (`Rig`, `RigBuilder`, `TwoBoneIKConstraint`, `BoneRenderer`), XRI selection on bone proxies

---

## File Map

**Create:**
- `Assets/Subsystems/RigBuilder/Data/BoneRecord.cs`
- `Assets/Subsystems/RigBuilder/Data/IkChainRecord.cs`
- `Assets/Subsystems/RigBuilder/Data/RigDefinition.cs`
- `Assets/Subsystems/RigBuilder/RigSerializer.cs`
- `Assets/Subsystems/RigBuilder/RigRuntime.cs`
- `Assets/Subsystems/RigBuilder/BoneProxy.cs`
- `Assets/Subsystems/RigBuilder/UI/BoneInspectorPanel.cs`
- `Assets/Subsystems/RigBuilder/UI/IkSetupWizard.cs`

**Unity Editor:**
- `BoneProxy.prefab` (sphere + collider + SelectionInteractor)
- `RigBuilderPanel.prefab` (World Space Canvas)

---

## Task 1: RigDefinition Data

**Files:** `RigBuilder/Data/BoneRecord.cs`, `RigBuilder/Data/RigDefinition.cs`, `RigBuilder/RigSerializer.cs`

- [ ] Create `Assets/Subsystems/RigBuilder/Data/BoneRecord.cs`:
  ```csharp
  using System;

  [Serializable]
  public class BoneRecord
  {
      public string BoneName;
      public bool TranslationLocked = true;
  }
  ```

- [ ] Create `Assets/Subsystems/RigBuilder/Data/IkChainRecord.cs`:
  ```csharp
  using System;

  [Serializable]
  public class IkChainRecord
  {
      public string RootBone;
      public string EndBone;
      public string PoleBone;   // optional, empty = no pole
      [UnityEngine.Range(0f, 1f)]
      public float Weight = 1f;
  }
  ```

- [ ] Create `Assets/Subsystems/RigBuilder/Data/RigDefinition.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  [Serializable]
  public class RigDefinition
  {
      public int SchemaVersion = 1;
      public string AssetId;
      public List<BoneRecord> Bones = new();
      public List<IkChainRecord> IkChains = new();
  }
  ```

- [ ] Create `Assets/Subsystems/RigBuilder/RigSerializer.cs`:
  ```csharp
  using UnityEngine;

  public static class RigSerializer
  {
      public static string Serialize(RigDefinition def) =>
          JsonUtility.ToJson(def, prettyPrint: true);

      public static RigDefinition Deserialize(string json) =>
          string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RigDefinition>(json);
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/RigBuilder/Data/ Assets/Subsystems/RigBuilder/RigSerializer.cs
  git commit -m "feat: add RigDefinition, BoneRecord, IkChainRecord, RigSerializer"
  ```

---

## Task 2: BoneProxy

**Files:** `RigBuilder/BoneProxy.cs`

- [ ] Create `Assets/Subsystems/RigBuilder/BoneProxy.cs`:
  ```csharp
  using UnityEngine;
  using VContainer;

  public class BoneProxy : MonoBehaviour
  {
      public string BoneName { get; private set; }
      public Transform BoneTransform { get; private set; }

      private SelectionManager _selectionManager;
      private string _nodeId;

      [Inject]
      public void Construct(SelectionManager selectionManager)
      {
          _selectionManager = selectionManager;
      }

      public void Init(string boneName, Transform boneTransform, string nodeId)
      {
          BoneName       = boneName;
          BoneTransform  = boneTransform;
          _nodeId        = nodeId;
          gameObject.name = $"Proxy_{boneName}";
      }

      private void Update()
      {
          // Follow the bone transform
          if (BoneTransform != null)
              transform.SetPositionAndRotation(BoneTransform.position, BoneTransform.rotation);
      }

      // Called by the SelectionInteractor component on this GameObject
      public void OnSelected() => _selectionManager.Select(_nodeId);
  }
  ```

- [ ] **In Unity Editor — create BoneProxy prefab:**
  1. Create GameObject → add `SphereCollider` (radius 0.02), `BoneProxy` script
  2. Add a small visual sphere as child (scale 0.04)
  3. Add `SelectionInteractor` component to the root
  4. Save as `Assets/Subsystems/RigBuilder/BoneProxy.prefab`
  5. Set layer to `BoneProxies` (add layer in Project Settings → Tags and Layers)
  6. On XR Ray Interactor: ensure `BoneProxies` layer is in the interaction layer mask

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/RigBuilder/BoneProxy.cs Assets/Subsystems/RigBuilder/
  git commit -m "feat: add BoneProxy — follows bone transform, selectable via XRI"
  ```

---

## Task 3: RigRuntime

**Files:** `RigBuilder/RigRuntime.cs`

> `RigRuntime` adds the Animation Rigging components to the selected SkinnedMesh's GameObject at runtime.  Unity's `Rig` + `RigBuilder` components require the GameObject to have an `Animator`. The Animation Rigging `BoneRenderer` draws lines between bones.

- [ ] Create `Assets/Subsystems/RigBuilder/RigRuntime.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.Animations.Rigging;
  using VContainer;

  public class RigRuntime : MonoBehaviour
  {
      [SerializeField] private GameObject _boneProxyPrefab;

      private IObjectResolver _container;
      private readonly List<BoneProxy> _proxies = new();

      [Inject]
      public void Construct(IObjectResolver container) => _container = container;

      public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
      {
          ClearProxies();

          var rigGo = new GameObject("_Rig");
          rigGo.transform.SetParent(smr.transform, worldPositionStays: false);

          var rig = rigGo.AddComponent<Rig>();

          // Ensure RigBuilder exists on the Animator's GameObject
          var animator = smr.GetComponentInParent<Animator>();
          if (animator == null) animator = smr.gameObject.AddComponent<Animator>();

          var rigBuilder = animator.gameObject.GetComponent<RigBuilder>();
          if (rigBuilder == null) rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
          rigBuilder.layers.Add(new RigLayer(rig));

          // Add BoneRenderer for visualization
          var boneRenderer = animator.gameObject.GetComponent<BoneRenderer>();
          if (boneRenderer == null) boneRenderer = animator.gameObject.AddComponent<BoneRenderer>();
          var transforms = new List<Transform>();
          foreach (var bone in definition.Bones)
          {
              var boneTr = FindBone(smr, bone.BoneName);
              if (boneTr != null) transforms.Add(boneTr);
          }
          boneRenderer.transforms = transforms.ToArray();

          // Spawn proxies
          foreach (var bone in definition.Bones)
          {
              var boneTr = FindBone(smr, bone.BoneName);
              if (boneTr == null) continue;

              var proxyGo = Instantiate(_boneProxyPrefab, boneTr.position, boneTr.rotation);
              _container.InjectGameObject(proxyGo);
              var proxy = proxyGo.GetComponent<BoneProxy>();
              var nodeId = $"bone_{bone.BoneName}";
              proxy.Init(bone.BoneName, boneTr, nodeId);
              _proxies.Add(proxy);
          }

          // Apply IK chains
          foreach (var chain in definition.IkChains)
              AddTwoBoneIK(rigGo.transform, smr, chain);

          rigBuilder.Build();
      }

      public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
      {
          var def = new RigDefinition { AssetId = smr.gameObject.name };
          foreach (var bone in smr.bones)
              def.Bones.Add(new BoneRecord { BoneName = bone.name });
          return def;
      }

      private void AddTwoBoneIK(Transform rigTransform, SkinnedMeshRenderer smr, IkChainRecord chain)
      {
          var ikGo  = new GameObject($"IK_{chain.RootBone}_{chain.EndBone}");
          ikGo.transform.SetParent(rigTransform, false);

          var constraint = ikGo.AddComponent<TwoBoneIKConstraint>();
          constraint.data.root = FindBone(smr, chain.RootBone);
          constraint.data.mid  = FindMidBone(smr, chain.RootBone, chain.EndBone);
          constraint.data.tip  = FindBone(smr, chain.EndBone);
          constraint.weight    = chain.Weight;

          // IK target: a simple empty GameObject the user can move
          var target = new GameObject($"Target_{chain.EndBone}");
          target.transform.SetParent(rigTransform, false);
          if (constraint.data.tip != null)
              target.transform.SetPositionAndRotation(constraint.data.tip.position, constraint.data.tip.rotation);
          constraint.data.target = target.transform;
      }

      private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
      {
          foreach (var b in smr.bones)
              if (b.name == boneName) return b;
          return null;
      }

      private Transform FindMidBone(SkinnedMeshRenderer smr, string root, string end)
      {
          // Return the first bone in the chain between root and end
          bool inChain = false;
          foreach (var b in smr.bones)
          {
              if (b.name == root) inChain = true;
              if (inChain && b.name != root && b.name != end) return b;
              if (b.name == end) break;
          }
          return null;
      }

      private void ClearProxies()
      {
          foreach (var p in _proxies)
              if (p != null) Destroy(p.gameObject);
          _proxies.Clear();
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/RigBuilder/RigRuntime.cs
  git commit -m "feat: add RigRuntime — applies Animation Rigging constraints and spawns BoneProxies"
  ```

---

## Task 4: BoneInspectorPanel + IkSetupWizard

**Files:** `RigBuilder/UI/BoneInspectorPanel.cs`, `RigBuilder/UI/IkSetupWizard.cs`

- [ ] Create `Assets/Subsystems/RigBuilder/UI/BoneInspectorPanel.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using TMPro;

  public class BoneInspectorPanel : SpatialPanel
  {
      [SerializeField] private Button _buildRigButton;
      [SerializeField] private TMP_Text _boneCountText;
      [SerializeField] private Button _openIkWizardButton;

      private RigRuntime _rigRuntime;
      private SelectionManager _selectionManager;
      private SceneGraph _sceneGraph;

      [Inject]
      public void Construct(RigRuntime rigRuntime, SelectionManager selectionManager, SceneGraph sceneGraph)
      {
          _rigRuntime       = rigRuntime;
          _selectionManager = selectionManager;
          _sceneGraph       = sceneGraph;
      }

      private void Awake()
      {
          _buildRigButton.onClick.AddListener(OnBuildRig);
          _openIkWizardButton.onClick.AddListener(OnOpenIkWizard);
      }

      private void OnBuildRig()
      {
          var nodeId = _selectionManager.SelectedNodeId;
          if (string.IsNullOrEmpty(nodeId)) return;

          var node = _sceneGraph.GetNode(nodeId);
          if (node == null) return;

          var smr = node.GetComponentInChildren<SkinnedMeshRenderer>();
          if (smr == null)
          {
              _boneCountText.text = "No SkinnedMeshRenderer found";
              return;
          }

          var def = _rigRuntime.BuildFromSkinnedMesh(smr);
          _rigRuntime.ApplyDefinition(def, smr);
          _boneCountText.text = $"{def.Bones.Count} bones";
      }

      private void OnOpenIkWizard()
      {
          // Open IkSetupWizard panel — wired via PanelRegistry or direct reference
          Debug.Log("Open IK Wizard");
      }
  }
  ```

- [ ] Create `Assets/Subsystems/RigBuilder/UI/IkSetupWizard.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using TMPro;

  public class IkSetupWizard : SpatialPanel
  {
      [SerializeField] private TMP_Dropdown _rootBoneDropdown;
      [SerializeField] private TMP_Dropdown _endBoneDropdown;
      [SerializeField] private Button _confirmButton;
      [SerializeField] private Button _cancelButton;

      private RigRuntime _rigRuntime;
      private SelectionManager _selectionManager;
      private SceneGraph _sceneGraph;

      private SkinnedMeshRenderer _currentSmr;
      private RigDefinition _currentDef;

      [Inject]
      public void Construct(RigRuntime rigRuntime, SelectionManager selectionManager, SceneGraph sceneGraph)
      {
          _rigRuntime       = rigRuntime;
          _selectionManager = selectionManager;
          _sceneGraph       = sceneGraph;
      }

      private void Awake()
      {
          _confirmButton.onClick.AddListener(OnConfirm);
          _cancelButton.onClick.AddListener(() => SetVisible(false));
      }

      public void OpenForSelection()
      {
          var node = _sceneGraph.GetNode(_selectionManager.SelectedNodeId);
          if (node == null) return;

          _currentSmr = node.GetComponentInChildren<SkinnedMeshRenderer>();
          if (_currentSmr == null) return;

          _currentDef = _rigRuntime.BuildFromSkinnedMesh(_currentSmr);

          PopulateDropdowns(_currentDef);
          SetVisible(true);
      }

      private void PopulateDropdowns(RigDefinition def)
      {
          var options = new List<TMP_Dropdown.OptionData>();
          foreach (var b in def.Bones)
              options.Add(new TMP_Dropdown.OptionData(b.BoneName));

          _rootBoneDropdown.ClearOptions();
          _endBoneDropdown.ClearOptions();
          _rootBoneDropdown.AddOptions(options);
          _endBoneDropdown.AddOptions(options);

          if (options.Count > 2)
              _endBoneDropdown.value = options.Count - 1;
      }

      private void OnConfirm()
      {
          if (_currentDef == null || _currentSmr == null) return;

          var chain = new IkChainRecord
          {
              RootBone = _rootBoneDropdown.options[_rootBoneDropdown.value].text,
              EndBone  = _endBoneDropdown.options[_endBoneDropdown.value].text,
              Weight   = 1f
          };
          _currentDef.IkChains.Add(chain);
          _rigRuntime.ApplyDefinition(_currentDef, _currentSmr);
          SetVisible(false);
      }
  }
  ```

- [ ] Register in `VrEditingSceneScope`:
  ```csharp
  builder.RegisterComponentInHierarchy<RigRuntime>();
  ```
  > `RigRuntime` is a MonoBehaviour — place it on a persistent GameObject in VrEditing.unity and register via hierarchy.

- [ ] **In Unity Editor:**
  1. Create RigBuilder panel prefab with "Build Rig" button, bone count label, "IK Wizard" button
  2. Create IkSetupWizard prefab with two dropdowns and Confirm/Cancel buttons
  3. Add both to `PanelRegistry.asset` (VisibleInModes = [VrEditing])
  4. Add `RigRuntime` component to a persistent `[Systems]` GameObject in VrEditing.unity; assign `_boneProxyPrefab`

- [ ] Press Play → import model → select it → open RigBuilder panel → click "Build Rig" → bone count appears, proxy spheres visible on bones → ray can select individual bones

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/RigBuilder/UI/
  git commit -m "feat: BoneInspectorPanel and IkSetupWizard — build rig, configure IK chains"
  ```

---

## Phase 6 Verification

- [ ] Import model → select → click "Build Rig" → `BoneRenderer` draws skeleton lines
- [ ] Bone proxy spheres appear; ray clicking a proxy fires `SelectionChangedEvent` with bone node ID
- [ ] IkSetupWizard opens, dropdowns populated with bone names, Confirm adds `TwoBoneIKConstraint`
- [ ] No Animation Rigging errors in Console (e.g., missing Animator)
