using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if USE_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

public class ARFoundationColorMapping : MonoBehaviour
{
    private class TrackableImageInfo
    {
        public GameObject arContent;
        public List<GameObject> coloringObj;
        public GameObject trackableObj;
        public Vector2 trackedImagePhysicalSize;
        public Vector2 trackedImageTextureSize;
    }

#if USE_ARFOUNDATION
    private ARTrackedImageManager trackedImageManager;
#endif

    [Tooltip("Please set the name of the prefab to be the same as the name of the marker image.")]
    [SerializeField]
    private GameObject[] prefabs;

    private Dictionary<string, TrackableImageInfo> trackables = new Dictionary<string, TrackableImageInfo>();
    private GameObject currentTrackable;

    [Header("Auto Run (no button needed)")]
    public bool autoRun = true;
    [Tooltip("Seconds to wait after the marker is detected before the first capture (lets tracking settle).")]
    public float startDelay = 0.3f;
    [Tooltip("How many captures to do after each detection, then stop. 1 = capture once.")]
    public int attemptsPerDetection = 1;
    [Tooltip("Seconds between those captures (only matters if attempts > 1).")]
    public float interval = 0.5f;
    [Tooltip("The marker must be gone for this many seconds before the next detection is treated as a NEW one. Short tracking flickers are ignored.")]
    public float lostResetTime = 1.0f;

    private GameObject lastTrackable;
    private float timer;
    private float lostTimer;
    private int attemptsDone;

#if USE_ARFOUNDATION
    void OnEnable()
    {
        trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>();

        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    void OnDestroy()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        List<ARTrackedImage> addedImages = eventArgs.added;
        List<ARTrackedImage> updatedImages = eventArgs.updated;
        List<ARTrackedImage> removedImages = eventArgs.removed;

        // Load
        foreach (ARTrackedImage added in addedImages)
        {
            string name = added.referenceImage.name;

            if (!trackables.ContainsKey(name))
            {
                GameObject arContent = Instantiate(prefabs.FirstOrDefault(prefab => prefab.name.Equals(name)), added.gameObject.transform);
                arContent.name = name;

                AddTrackable(arContent, added);
            }
        }

        // Found/Lost
        foreach (ARTrackedImage updated in updatedImages)
        {
            string name = updated.referenceImage.name;

            if (trackables.ContainsKey(name))
            {
                TrackableImageInfo trackable = trackables[name];
                GameObject arContent = trackable.arContent;

                if (updated.trackingState == TrackingState.Tracking)
                {
                    if (currentTrackable != null)
                    {
                        if (!currentTrackable.name.Equals(name))
                        {
                            currentTrackable.SetActive(false);

                            currentTrackable = arContent;
                            currentTrackable.SetActive(true);
                        }
                    }
                    else
                    {
                        currentTrackable = arContent;
                        currentTrackable.SetActive(true);
                    }
                }
                else
                {
                    if (arContent.activeSelf)
                    {
                        arContent.SetActive(false);
                        currentTrackable = null;
                    }
                }
            }
        }
    }
#endif

    public void AddTrackable(GameObject arContent, ARTrackedImage trackedImage)
    {
        string name = trackedImage.referenceImage.name;

        List<GameObject> coloringObj = GetChildWithTag(arContent.transform, "coloring", true);

        if (coloringObj == null || coloringObj.Count == 0)
        {
            return;
        }

        TrackableImageInfo trackableImage = new TrackableImageInfo
        {
            arContent = arContent,
            coloringObj = coloringObj,
            trackedImagePhysicalSize = trackedImage.size,
            trackedImageTextureSize = new Vector2(trackedImage.referenceImage.texture.width, trackedImage.referenceImage.texture.height)
        };

        trackableImage.trackableObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trackableImage.trackableObj.SetActive(false);
        trackableImage.trackableObj.name = "AirarTrackable";
        // Parent the invisible marker-size cube to the tracked image itself (NOT to the prefab root),
        // so the prefab's own scale/rotation can't change the size/orientation of the capture area.
        trackableImage.trackableObj.transform.SetParent(trackedImage.transform);
        trackableImage.trackableObj.transform.localPosition = Vector3.zero;
        trackableImage.trackableObj.transform.localRotation = Quaternion.Euler(Vector3.zero);
        trackableImage.trackableObj.transform.localScale = new Vector3(trackableImage.trackedImagePhysicalSize.x, 0.001f, trackableImage.trackedImagePhysicalSize.y);

        trackables.Add(name, trackableImage);
    }

    void Update()
    {
        if (currentTrackable == null)
        {
            // Marker not visible. Only after it has been gone for a while do we
            // forget it, so the next detection counts as new. Short flickers are ignored.
            lostTimer += Time.deltaTime;
            if (lostTimer >= lostResetTime) lastTrackable = null;
            return;
        }
        lostTimer = 0f;

        // New marker detected (or a different marker) -> start over
        if (currentTrackable != lastTrackable)
        {
            lastTrackable = currentTrackable;
            timer = 0f;
            attemptsDone = 0;
        }

        if (!autoRun) return;
        if (attemptsDone >= attemptsPerDetection) return;   // done for this detection -> stop

        timer += Time.deltaTime;
        float wait = (attemptsDone == 0) ? startDelay : interval;
        if (timer < wait) return;

        timer = 0f;
        attemptsDone++;
        Play();
    }

    public void Play()
    {
        if (currentTrackable == null) return;

        if (trackables.ContainsKey(currentTrackable.name))
        {
            TrackableImageInfo trackable = trackables[currentTrackable.name];

            if (trackable.trackableObj == null)
            {
                return;
            }

            Texture2D tempTex = ScreenShot.GetTmpTex(currentTrackable);
            float[] srcValue = AirarManager.Instance.CalculateMarkerImageVertex(trackable.trackableObj);

            AirarManager.Instance.ProcessColoredMapTexture(tempTex, srcValue, (int)trackable.trackedImageTextureSize.x, (int)trackable.trackedImageTextureSize.y, (texture) =>
            {
                if (texture != null)
                {
                    foreach (GameObject coloringObj in trackable.coloringObj)
                    {
                        coloringObj.GetComponent<Renderer>().material.mainTexture = texture;
                    }
                }
            });
        }
    }

    private List<GameObject> GetChildWithTag(Transform obj, string tag, bool checkInactive = false)
    {
        List<GameObject> list_Obj = new List<GameObject>();

        Transform[] trs = obj.GetComponentsInChildren<Transform>(checkInactive);
        foreach (Transform t in trs)
        {
            if (t.gameObject.CompareTag(tag))
            {
                list_Obj.Add(t.gameObject);
            }
        }

        return list_Obj;
    }
}