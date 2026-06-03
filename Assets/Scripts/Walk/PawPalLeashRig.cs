using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PawPalLeashRig : MonoBehaviour
{
    private const string LeashRootName = "leash";
    private const string LeashBodyGroupName = "leashbody";
    private const string DogLeashRootName = "Dog_Leash_Root";
    private const string HandleOuterTealName = "Handle_Outer_Teal";
    private const string HandleInnerGripName = "Handle_Inner_Black_Grip";
    private const string LeashStartAnchorName = "Leash_Start_Anchor";
    private const string LeashEndAnchorName = "Leash_End_Anchor";
    private const string LeashBodyName = "Leash_Body";
    private const string EditorFallbackLeashAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/leash.fbx";
    private const float DefaultLeashScaleFactor = 0.5f;

    [SerializeField] private Transform leashRoot;
    [SerializeField] private Transform dragRoot;
    [SerializeField] private Transform handleGrabTarget;
    [SerializeField] private Transform leashStartAnchor;
    [SerializeField] private Transform leashEndAnchor;
    [SerializeField] private Renderer[] staticStrapRenderers = new Renderer[0];
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private Color ropeColor = new Color(0.109f, 0.279f, 0.858f, 1f);
    [SerializeField, Min(0.005f)] private float ropeWidth = 0.03f;
    [SerializeField, Min(2)] private int segmentCount = 18;
    [SerializeField, Min(0.01f)] private float segmentLength = 0.08f;
    [SerializeField, Range(0f, 1f)] private float damping = 0.985f;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField, Min(1)] private int solverIterations = 8;
    [SerializeField] private string targetDogId = string.Empty;
    [SerializeField] private bool autoResolveSceneReferences = true;

    private PawPalGameRuntime runtime;
    private PawPalDogSceneBridge dogSceneBridge;
    private Transform leashSocket;
    private string resolvedDogId = string.Empty;
    private LineRenderer ropeRenderer;
    private Material runtimeRopeMaterial;
    private bool leashRootAlignedToSocket;
    private bool handleDragActive;
    private bool authoredBaseScaleInitialized;
    private Vector3 authoredBaseRootLocalScale = Vector3.one;
    private Vector3 defaultDragRootOffset = new Vector3(-0.04f, 0.62f, 0.08f);

    public Transform DragRoot => dragRoot != null ? dragRoot : leashRoot;
    public Transform HandleGrabTarget => handleGrabTarget != null ? handleGrabTarget : leashRoot;
    public Transform LeashStartAnchor => leashStartAnchor;
    public Transform LeashEndAnchor => leashEndAnchor;

    public static bool TryInstallSceneRig()
    {
        PawPalLeashRig existingRig = Object.FindFirstObjectByType<PawPalLeashRig>(FindObjectsInactive.Include);
        if (existingRig != null)
        {
            existingRig.ResolveReferencesIfNeeded();
            existingRig.EnsureRuntimeSetup();
            EnsureDragController(existingRig);
            return true;
        }

        if (!TryResolveSceneRigReferences(
            out Transform resolvedLeashRoot,
            out Transform resolvedDragRoot,
            out Transform resolvedHandleGrabTarget,
            out Transform resolvedLeashStartAnchor,
            out Transform resolvedLeashEndAnchor,
            out Renderer[] resolvedStaticStrapRenderers))
        {
            if (!TryInstantiateEditorFallbackLeash()
                || !TryResolveSceneRigReferences(
                    out resolvedLeashRoot,
                    out resolvedDragRoot,
                    out resolvedHandleGrabTarget,
                    out resolvedLeashStartAnchor,
                    out resolvedLeashEndAnchor,
                    out resolvedStaticStrapRenderers))
            {
                Debug.LogWarning(
                    "PawPalLeashRig could not find a leash object in the active walking scene. " +
                    "Save a leash instance into Walking.unity or keep '" + EditorFallbackLeashAssetPath + "' available for editor fallback.");
                return false;
            }
        }

        PawPalLeashRig rig = resolvedLeashRoot.gameObject.AddComponent<PawPalLeashRig>();
        rig.ConfigureResolvedReferences(
            resolvedLeashRoot,
            resolvedDragRoot,
            resolvedHandleGrabTarget,
            resolvedLeashStartAnchor,
            resolvedLeashEndAnchor,
            resolvedStaticStrapRenderers);
        rig.ResolveReferencesIfNeeded();
        rig.EnsureRuntimeSetup();
        EnsureDragController(rig);
        return true;
    }

    public void ConfigureResolvedReferences(
        Transform resolvedLeashRoot,
        Transform resolvedDragRoot,
        Transform resolvedHandleGrabTarget,
        Transform resolvedLeashStartAnchor,
        Transform resolvedLeashEndAnchor,
        Renderer[] resolvedStaticStrapRenderers)
    {
        leashRoot = resolvedLeashRoot;
        dragRoot = resolvedDragRoot;
        handleGrabTarget = resolvedHandleGrabTarget;
        leashStartAnchor = resolvedLeashStartAnchor;
        leashEndAnchor = resolvedLeashEndAnchor;
        staticStrapRenderers = resolvedStaticStrapRenderers ?? new Renderer[0];
        leashRootAlignedToSocket = false;
        authoredBaseScaleInitialized = false;
    }

    public void SetDragRootPosition(Vector3 worldPosition)
    {
        Transform target = DragRoot;
        if (target == null)
        {
            return;
        }

        target.position = worldPosition;
    }

    public void SetHandleDragActive(bool isActive)
    {
        handleDragActive = isActive;
    }

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSetup();
    }

    private void OnEnable()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSetup();
    }

    private void OnDisable()
    {
        if (runtimeRopeMaterial != null)
        {
            Destroy(runtimeRopeMaterial);
            runtimeRopeMaterial = null;
        }
    }

    private void FixedUpdate()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSetup();
        SyncEndAnchorToSocket();
    }

    private void LateUpdate()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSetup();
        SyncEndAnchorToSocket();
        UpdateLineRenderer();
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(2, segmentCount);
        segmentLength = Mathf.Max(0.01f, segmentLength);
        ropeWidth = Mathf.Max(0.005f, ropeWidth);
        solverIterations = Mathf.Max(1, solverIterations);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (!autoResolveSceneReferences)
        {
            return;
        }

        if (leashRoot == null)
        {
            leashRoot = FindNamedTransformInActiveScene(LeashRootName);
            if (leashRoot == null)
            {
                leashRoot = FindNamedTransformInActiveScene(DogLeashRootName);
            }
        }

        if (leashStartAnchor == null)
        {
            leashStartAnchor = FindNamedTransformInScope(LeashStartAnchorName);
        }

        if (leashEndAnchor == null)
        {
            leashEndAnchor = FindNamedTransformInScope(LeashEndAnchorName);
        }

        if (handleGrabTarget == null)
        {
            handleGrabTarget = FindNamedTransformInScope(HandleOuterTealName);
            if (handleGrabTarget == null)
            {
                handleGrabTarget = FindNamedTransformInScope(HandleInnerGripName);
            }
        }

        if (dragRoot == null)
        {
            dragRoot = FindNamedTransformInScope(LeashBodyGroupName);
            if (dragRoot == null)
            {
                dragRoot = leashRoot;
            }
        }

        if ((staticStrapRenderers == null || staticStrapRenderers.Length == 0) && leashRoot != null)
        {
            List<Renderer> renderers = new List<Renderer>();
            Renderer[] candidates = leashRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Renderer candidate = candidates[i];
                if (candidate == null || !IsLeashBodyRenderer(candidate))
                {
                    continue;
                }

                renderers.Add(candidate);
            }

            staticStrapRenderers = renderers.ToArray();
        }

        if (!authoredBaseScaleInitialized && DragRoot != null)
        {
            authoredBaseRootLocalScale = DragRoot.localScale;
            authoredBaseScaleInitialized = true;
        }
    }

    private void EnsureRuntimeSetup()
    {
        EnsureLeashScale();
        DisableLeashBodyRenderers();
        EnsureLineRenderer();
        EnsureDragColliders();
        EnsureDragRootCollider();
        EnsureBridge();
        ResolveLeashSocket();
        FollowSocketWithDragRootWhenIdle();
        SyncStartAnchorToDragRoot();
    }

    private void EnsureBridge()
    {
        runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            dogSceneBridge = runtime.GetComponent<PawPalDogSceneBridge>();
        }

        if (dogSceneBridge == null)
        {
            dogSceneBridge = Object.FindFirstObjectByType<PawPalDogSceneBridge>(FindObjectsInactive.Include);
        }
    }

    private void ResolveLeashSocket()
    {
        if (dogSceneBridge == null)
        {
            leashSocket = null;
            return;
        }

        string nextDogId = !string.IsNullOrWhiteSpace(targetDogId)
            ? targetDogId.Trim()
            : (runtime != null && runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty);
        if (string.IsNullOrWhiteSpace(nextDogId))
        {
            leashSocket = null;
            resolvedDogId = string.Empty;
            return;
        }

        if (nextDogId == resolvedDogId && leashSocket != null)
        {
            return;
        }

        resolvedDogId = nextDogId;
        leashRootAlignedToSocket = false;
        if (!dogSceneBridge.TryGetLeashSocket(resolvedDogId, out leashSocket))
        {
            leashSocket = null;
        }
    }

    private void SyncEndAnchorToSocket()
    {
        if (leashEndAnchor == null || leashSocket == null)
        {
            return;
        }

        leashEndAnchor.SetPositionAndRotation(leashSocket.position, leashSocket.rotation);
    }

    private void FollowSocketWithDragRootWhenIdle()
    {
        if (handleDragActive || leashSocket == null)
        {
            return;
        }

        Transform root = DragRoot;
        if (root == null)
        {
            return;
        }

        root.position = leashSocket.position + defaultDragRootOffset;
        leashRootAlignedToSocket = true;
    }

    private void SyncStartAnchorToDragRoot()
    {
        if (leashStartAnchor == null)
        {
            return;
        }

        Transform root = DragRoot;
        if (root == null)
        {
            return;
        }

        leashStartAnchor.SetPositionAndRotation(root.position, root.rotation);
    }

    private void EnsureLeashScale()
    {
        Transform root = DragRoot;
        if (root == null)
        {
            return;
        }

        if (!authoredBaseScaleInitialized)
        {
            authoredBaseRootLocalScale = root.localScale;
            authoredBaseScaleInitialized = true;
        }

        Vector3 targetScale = authoredBaseRootLocalScale * DefaultLeashScaleFactor;
        if (root.localScale != targetScale)
        {
            root.localScale = targetScale;
            leashRootAlignedToSocket = false;
        }
    }

    private bool HasValidAnchors()
    {
        return leashStartAnchor != null && leashEndAnchor != null;
    }

    private void DisableLeashBodyRenderers()
    {
        if (staticStrapRenderers == null)
        {
            return;
        }

        for (int i = 0; i < staticStrapRenderers.Length; i++)
        {
            Renderer renderer = staticStrapRenderers[i];
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
            }
        }
    }

    private void EnsureDragColliders()
    {
        if (leashRoot == null)
        {
            return;
        }

        Renderer[] renderers = leashRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldAddDragCollider(renderer))
            {
                continue;
            }

            Transform target = renderer.transform;
            Collider existing = target.GetComponent<Collider>();
            if (existing != null)
            {
                continue;
            }

            SphereCollider collider = target.gameObject.AddComponent<SphereCollider>();
            Bounds bounds = renderer.bounds;
            collider.center = target.InverseTransformPoint(bounds.center);
            collider.radius = Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));
        }
    }

    private void EnsureDragRootCollider()
    {
        Transform target = DragRoot;
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        Bounds combinedBounds = default(Bounds);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = target.gameObject.AddComponent<BoxCollider>();
        }

        collider.center = target.InverseTransformPoint(combinedBounds.center);
        collider.size = new Vector3(
            Mathf.Max(0.05f, combinedBounds.size.x),
            Mathf.Max(0.05f, combinedBounds.size.y),
            Mathf.Max(0.05f, combinedBounds.size.z));
    }

    private void EnsureLineRenderer()
    {
        if (ropeRenderer == null)
        {
            ropeRenderer = GetComponent<LineRenderer>();
        }

        if (ropeRenderer == null)
        {
            ropeRenderer = gameObject.AddComponent<LineRenderer>();
        }

        ropeRenderer.enabled = true;
        ropeRenderer.useWorldSpace = true;
        ropeRenderer.widthMultiplier = ropeWidth;
        ropeRenderer.numCapVertices = 4;
        ropeRenderer.positionCount = Mathf.Max(4, segmentCount + 1);
        ropeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ropeRenderer.receiveShadows = false;
        ropeRenderer.textureMode = LineTextureMode.Stretch;
        ropeRenderer.alignment = LineAlignment.View;
        ropeRenderer.startColor = ropeColor;
        ropeRenderer.endColor = ropeColor;
        ropeRenderer.material = ResolveRopeMaterial();
    }

    private Material ResolveRopeMaterial()
    {
        if (ropeMaterial != null)
        {
            return ropeMaterial;
        }

        if (runtimeRopeMaterial != null)
        {
            return runtimeRopeMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeRopeMaterial = new Material(shader);
        runtimeRopeMaterial.color = ropeColor;
        return runtimeRopeMaterial;
    }

    private void UpdateLineRenderer()
    {
        if (ropeRenderer == null || leashStartAnchor == null)
        {
            return;
        }

        Vector3 start = leashStartAnchor.position;
        Vector3 end = leashSocket != null ? leashSocket.position : (leashEndAnchor != null ? leashEndAnchor.position : start);
        int pointCount = Mathf.Max(4, segmentCount + 1);
        ropeRenderer.positionCount = pointCount;

        float sag = Mathf.Clamp(Vector3.Distance(start, end) * 0.18f, 0.08f, 0.3f);
        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : i / (float)(pointCount - 1);
            Vector3 position = Vector3.Lerp(start, end, t);
            position += Vector3.down * (4f * t * (1f - t) * sag);
            ropeRenderer.SetPosition(i, position);
        }
    }

    private static void EnsureDragController(PawPalLeashRig rig)
    {
        if (rig == null)
        {
            return;
        }

        PawPalLeashDragController controller = rig.GetComponent<PawPalLeashDragController>();
        if (controller == null)
        {
            controller = rig.gameObject.AddComponent<PawPalLeashDragController>();
        }

        controller.Configure(rig, rig.HandleGrabTarget);
    }

    private static bool TryResolveSceneRigReferences(
        out Transform resolvedLeashRoot,
        out Transform resolvedDragRoot,
        out Transform resolvedHandleGrabTarget,
        out Transform resolvedLeashStartAnchor,
        out Transform resolvedLeashEndAnchor,
        out Renderer[] resolvedStaticStrapRenderers)
    {
        resolvedLeashRoot = null;
        resolvedDragRoot = null;
        resolvedHandleGrabTarget = null;
        resolvedLeashStartAnchor = FindNamedTransformInActiveScene(LeashStartAnchorName);
        resolvedLeashEndAnchor = FindNamedTransformInActiveScene(LeashEndAnchorName);
        resolvedStaticStrapRenderers = new Renderer[0];
        if (resolvedLeashStartAnchor == null || resolvedLeashEndAnchor == null)
        {
            return false;
        }

        resolvedHandleGrabTarget = FindNamedTransformInActiveScene(HandleOuterTealName);
        if (resolvedHandleGrabTarget == null)
        {
            resolvedHandleGrabTarget = FindNamedTransformInActiveScene(HandleInnerGripName);
        }

        resolvedLeashRoot = FindNamedTransformInActiveScene(LeashRootName);
        if (resolvedLeashRoot == null)
        {
            resolvedLeashRoot = FindNamedTransformInActiveScene(DogLeashRootName);
        }

        if (resolvedLeashRoot == null)
        {
            resolvedLeashRoot = ResolveCommonAncestor(resolvedLeashStartAnchor, resolvedLeashEndAnchor, resolvedHandleGrabTarget);
        }

        if (resolvedLeashRoot == null)
        {
            return false;
        }

        resolvedDragRoot = FindNamedTransformInHierarchy(resolvedLeashRoot, LeashBodyGroupName);
        if (resolvedDragRoot == null)
        {
            resolvedDragRoot = resolvedLeashRoot;
        }
        return true;
    }

    private static bool IsLeashBodyRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string candidateName = renderer.name;
        return candidateName.IndexOf("Leash_Strap", System.StringComparison.OrdinalIgnoreCase) >= 0
            || candidateName.IndexOf("Blue_Nylon_Leash", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldAddDragCollider(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
        {
            return false;
        }

        string candidateName = renderer.name;
        return candidateName.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || candidateName.IndexOf("Connector", System.StringComparison.OrdinalIgnoreCase) >= 0
            || candidateName.IndexOf("Leash_Body_Teal", System.StringComparison.OrdinalIgnoreCase) >= 0
            || candidateName.IndexOf("Top_Button", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryInstantiateEditorFallbackLeash()
    {
#if UNITY_EDITOR
        GameObject leashAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorFallbackLeashAssetPath);
        if (leashAsset == null)
        {
            return false;
        }

        GameObject instance = Object.Instantiate(leashAsset);
        if (instance == null)
        {
            return false;
        }

        instance.name = "WalkRuntimeLeash";
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(instance, activeScene);
        }

        return true;
#else
        return false;
#endif
    }

    private Transform FindNamedTransformInScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (leashRoot != null)
        {
            Transform[] children = leashRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == name)
                {
                    return children[i];
                }
            }
        }

        return FindNamedTransformInActiveScene(name);
    }

    private static Transform FindNamedTransformInHierarchy(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null
                && string.Equals(candidate.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindNamedTransformInActiveScene(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null
                || candidate.gameObject.scene != activeScene
                || candidate.name != name)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static Transform ResolveCommonAncestor(Transform first, Transform second, Transform third)
    {
        if (first == null || second == null)
        {
            return null;
        }

        HashSet<Transform> ancestors = new HashSet<Transform>();
        Transform current = first;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.parent;
        }

        current = second;
        while (current != null)
        {
            if (ancestors.Contains(current) && (third == null || third.IsChildOf(current) || third == current))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}
