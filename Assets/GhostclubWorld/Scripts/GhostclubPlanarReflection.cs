using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ghostclub
{
    /// <summary>
    /// Real-time Planar Mirror Reflection for the Ghostclub 5.0 dancefloor.
    /// Renders a mirrored camera below Y=0 (ignoring Layer 4 'Water' so floor geometry never blocks the view)
    /// into a RenderTexture and feeds it to Mat_FloorReflection.
    /// </summary>
    [ExecuteAlways]
    public class GhostclubPlanarReflection : MonoBehaviour
    {
        public Material floorReflectionMaterial;
        public float floorHeight = 0.0f;
        [Range(0f, 1f)] public float reflectionIntensity = 0.62f;

        private Camera _reflectionCam;
        private RenderTexture _reflectionRT;

        void OnEnable()
        {
            EnsureReflectionResources();
        }

        void EnsureReflectionResources()
        {
            if (_reflectionRT == null)
            {
                _reflectionRT = new RenderTexture(1024, 576, 24, RenderTextureFormat.DefaultHDR);
                _reflectionRT.name = "Ghostclub_FloorReflection_RT";
                _reflectionRT.useMipMap = false;
                _reflectionRT.Create();
            }

            if (_reflectionCam == null)
            {
                Transform existing = transform.Find("Planar_Reflection_Camera");
                GameObject camGo = existing != null ? existing.gameObject : new GameObject("Planar_Reflection_Camera");
                camGo.transform.SetParent(transform, false);
                camGo.hideFlags = HideFlags.DontSave;

                _reflectionCam = camGo.GetComponent<Camera>();
                if (_reflectionCam == null) _reflectionCam = camGo.AddComponent<Camera>();

                _reflectionCam.enabled = true;
                _reflectionCam.depth = -10f;
                _reflectionCam.targetTexture = _reflectionRT;
                _reflectionCam.clearFlags = CameraClearFlags.SolidColor;
                _reflectionCam.backgroundColor = Color.black;
                _reflectionCam.nearClipPlane = 0.1f;
                _reflectionCam.farClipPlane = 60f;
                // Exclude Layer 4 (Water / Floor meshes) so the camera below Y=0 sees unobstructed into the club
                _reflectionCam.cullingMask = ~(1 << 4);

                var camData = camGo.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = camGo.AddComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = false;
                    camData.renderShadows = false;
                }
            }

            if (_reflectionCam != null && _reflectionCam.targetTexture != _reflectionRT)
            {
                _reflectionCam.targetTexture = _reflectionRT;
                _reflectionCam.depth = -10f;
            }

            if (floorReflectionMaterial != null && _reflectionRT != null)
            {
                floorReflectionMaterial.SetTexture("_ReflectionTex", _reflectionRT);
            }
        }

        void LateUpdate()
        {
            EnsureReflectionResources();

            Camera mainCam = Camera.main;
            if (mainCam == null || _reflectionCam == null) return;

            if (_reflectionCam.targetTexture != _reflectionRT)
            {
                _reflectionCam.targetTexture = _reflectionRT;
            }

            Vector3 camPos = mainCam.transform.position;
            Vector3 camEuler = mainCam.transform.eulerAngles;

            // Mirror camera position & pitch across Y = floorHeight
            float distAboveFloor = camPos.y - floorHeight;
            _reflectionCam.transform.position = new Vector3(camPos.x, floorHeight - distAboveFloor, camPos.z);
            _reflectionCam.transform.eulerAngles = new Vector3(-camEuler.x, camEuler.y, -camEuler.z);
            _reflectionCam.fieldOfView = mainCam.fieldOfView;
            _reflectionCam.aspect = mainCam.aspect;

            if (floorReflectionMaterial != null)
            {
                floorReflectionMaterial.SetTexture("_ReflectionTex", _reflectionRT);
                floorReflectionMaterial.SetFloat("_ReflectionIntensity", reflectionIntensity);
            }
        }

        void OnDisable()
        {
            if (_reflectionCam != null)
            {
                _reflectionCam.targetTexture = null;
                DestroyImmediate(_reflectionCam.gameObject);
                _reflectionCam = null;
            }
            if (_reflectionRT != null)
            {
                _reflectionRT.Release();
                DestroyImmediate(_reflectionRT);
                _reflectionRT = null;
            }
        }
    }
}
