using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

namespace Ghostclub.Editor
{
    public static class GhostclubSceneBuilder
    {
        [MenuItem("Ghostclub/Build Ghostclub 5.0 Scene")]
        public static void BuildScene()
        {
            Debug.Log("[Ghostclub] Building 80-Avatar VRChat Ghostclub 5.0 Scene (Reflective Floor + Room-Distributed AudioLink Moving Heads + Video Mood Lighting + 1x CDJ-3000 + Individual Wall Posters)...");

            // 1. Verify Textures
            GhostclubPBRTextureGenerator.GeneratePBRTextures();

            string matDir = "Assets/GhostclubWorld/Materials";
            if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

            // 2. Create Persistent Material Assets (Prevents Shader Stripping in Standalone Mac Build!)
            Shader beamShader = Shader.Find("Ghostclub/VolumetricBeam");
            if (beamShader == null) beamShader = Shader.Find("Sprites/Default");
            Material sharedBeamMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_VolumetricBeam.mat", beamShader);
            if (sharedBeamMat.HasProperty("_Color")) sharedBeamMat.SetColor("_Color", new Color(0f, 0.5f, 1f, 0.35f));
            if (sharedBeamMat.HasProperty("_FalloffPower")) sharedBeamMat.SetFloat("_FalloffPower", 2.2f);

            Material sharedLaserMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_LaserBeam.mat", beamShader);
            if (sharedLaserMat.HasProperty("_Color")) sharedLaserMat.SetColor("_Color", new Color(0f, 0.55f, 1f, 0.95f));

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Texture2D concreteNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GhostclubWorld/Textures/Concrete_Normal.png");
            Texture2D cdjScreenTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GhostclubWorld/Textures/CDJ_Screen_Waveform.png");
            Texture2D djmFaceplateTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GhostclubWorld/Textures/DJM_Mixer_Faceplate.png");
            Texture2D speakerGrilleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GhostclubWorld/Textures/FunktionOne_Grille_Normal.png");

            // Floor Planar Reflection Material Asset
            Shader floorReflShader = Shader.Find("Ghostclub/FloorReflection");
            if (floorReflShader == null) floorReflShader = urpLit;
            Material floorReflMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_FloorReflection.mat", floorReflShader);
            if (concreteNormal != null && floorReflMat.HasProperty("_BumpMap"))
            {
                floorReflMat.SetTexture("_BumpMap", concreteNormal);
                floorReflMat.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
            }
            if (floorReflMat.HasProperty("_ReflectionIntensity")) floorReflMat.SetFloat("_ReflectionIntensity", 0.65f);
            if (floorReflMat.HasProperty("_Distortion")) floorReflMat.SetFloat("_Distortion", 0.010f);
            EditorUtility.SetDirty(floorReflMat);

            Material videoWallMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_VideoWall.mat", urpLit);
            SetMatColor(videoWallMat, Color.white);
            if (videoWallMat.HasProperty("_Smoothness")) videoWallMat.SetFloat("_Smoothness", 0.2f);
            if (videoWallMat.HasProperty("_Cull")) videoWallMat.SetFloat("_Cull", 0f);
            SetMatEmission(videoWallMat, Color.white * 2.2f);

            Material moverBodyMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_MovingHead_Body.mat", urpLit);
            SetMatColor(moverBodyMat, new Color(0.14f, 0.16f, 0.20f, 1f));
            if (moverBodyMat.HasProperty("_Metallic")) moverBodyMat.SetFloat("_Metallic", 0.88f);
            if (moverBodyMat.HasProperty("_Smoothness")) moverBodyMat.SetFloat("_Smoothness", 0.72f);
            SetMatEmission(moverBodyMat, new Color(0.02f, 0.05f, 0.12f));

            Material moverLensBlueMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_MovingHead_Lens_Blue.mat", urpLit);
            Color cyberBlue = new Color(0.0f, 0.50f, 1.0f, 1f);
            SetMatColor(moverLensBlueMat, cyberBlue);
            SetMatEmission(moverLensBlueMat, cyberBlue * 22f);

            Material moverLensRedMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_MovingHead_Lens_Red.mat", urpLit);
            Color bloodRed = new Color(1.0f, 0.02f, 0.10f, 1f);
            SetMatColor(moverLensRedMat, bloodRed);
            SetMatEmission(moverLensRedMat, bloodRed * 22f);

            Material cdjScreenMatAsset = CreateOrUpdateMaterialAsset($"{matDir}/Mat_CDJ3000_Screen.mat", urpLit);
            SetMatColor(cdjScreenMatAsset, Color.white);
            SetMatTexture(cdjScreenMatAsset, cdjScreenTex);
            SetMatEmission(cdjScreenMatAsset, Color.white * 1.4f, cdjScreenTex);

            Material djmFaceMatAsset = CreateOrUpdateMaterialAsset($"{matDir}/Mat_DJMA9_Faceplate.mat", urpLit);
            SetMatColor(djmFaceMatAsset, Color.white);
            SetMatTexture(djmFaceMatAsset, djmFaceplateTex);
            SetMatEmission(djmFaceMatAsset, Color.white * 1.25f, djmFaceplateTex);

            // 3. Create Fresh Scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Load & Instantiate World Geometry
            string fbxPath = "Assets/GhostclubWorld/Models/ghostclub_5_0.fbx";
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"[Ghostclub] Model not found at path: {fbxPath}.");
                return;
            }

            GameObject worldInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            worldInstance.name = "Ghostclub_World_Geometry";
            worldInstance.transform.position = Vector3.zero;
            worldInstance.transform.rotation = Quaternion.identity;

            // 5. Setup World Materials & Colliders
            MeshRenderer[] renderers = worldInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mr in renderers)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                string objName = mr.gameObject.name.ToLower();

                if (objName.Contains("floor") || objName.Contains("wall") ||
                    objName.Contains("pillar") || objName.Contains("col") ||
                    objName.Contains("stage") || objName.Contains("step") ||
                    objName.Contains("booth") || objName.Contains("table") ||
                    objName.Contains("curtain") || objName.Contains("drape") ||
                    objName.Contains("speaker") || objName.Contains("funktion") ||
                    objName.Contains("vj_frame"))
                {
                    if (mr.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                }

                // Put Floor meshes on Layer 4 (Water) so the Planar Reflection Camera below Y=0 sees unobstructed through the floor!
                if (objName.Contains("floor"))
                {
                    mr.gameObject.layer = 4;
                }

                Material[] sharedMats = mr.sharedMaterials;
                bool matsUpdated = false;
                for (int m = 0; m < sharedMats.Length; m++)
                {
                    Material mat = sharedMats[m];
                    if (mat == null) continue;
                    string matName = mat.name.ToLower();

                    if (matName.Contains("cdj_screen") || matName.Contains("laptop_screen"))
                    {
                        sharedMats[m] = cdjScreenMatAsset;
                        matsUpdated = true;
                        continue;
                    }
                    else if (matName.Contains("vumeter"))
                    {
                        sharedMats[m] = djmFaceMatAsset;
                        matsUpdated = true;
                        continue;
                    }
                    else if (matName.Contains("vj_screen"))
                    {
                        sharedMats[m] = videoWallMat;
                        matsUpdated = true;
                        continue;
                    }

                    // Raw Brutalist Concrete
                    if (matName.Contains("concrete_raw") || objName.Contains("wall") || objName.Contains("pillar") || objName.Contains("beam"))
                    {
                        SetMatColor(mat, new Color(0.09f, 0.10f, 0.12f, 1f));
                        if (concreteNormal != null && mat.HasProperty("_BumpMap"))
                        {
                            mat.SetTexture("_BumpMap", concreteNormal);
                            mat.EnableKeyword("_NORMALMAP");
                            mat.SetTextureScale("_BumpMap", new Vector2(3f, 3f));
                        }
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                    }
                    // Polished Reflective Dancefloor
                    else if (matName.Contains("concrete_polished") || objName.Contains("dancefloor"))
                    {
                        SetMatColor(mat, new Color(0.015f, 0.018f, 0.025f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.65f);
                        if (concreteNormal != null && mat.HasProperty("_BumpMap"))
                        {
                            mat.SetTexture("_BumpMap", concreteNormal);
                            mat.EnableKeyword("_NORMALMAP");
                            mat.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
                            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.08f);
                        }
                    }
                    // DJ Chassis & Steel
                    else if (matName.Contains("dj_chassis"))
                    {
                        SetMatColor(mat, new Color(0.08f, 0.09f, 0.11f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.78f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.90f);
                    }
                    else if (matName.Contains("steel") || objName.Contains("truss") || objName.Contains("booth"))
                    {
                        SetMatColor(mat, new Color(0.04f, 0.045f, 0.055f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.75f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.92f);
                    }
                    // Pleated Curtain Drape
                    else if (matName.Contains("curtain_pleated") || objName.Contains("drape"))
                    {
                        SetMatColor(mat, new Color(0.04f, 0.045f, 0.055f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.30f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
                    }
                    // Chrome Knobs, Faders & Flight-Case Trim
                    else if (matName.Contains("flightcase_chrome") || matName.Contains("chrome"))
                    {
                        SetMatColor(mat, new Color(0.78f, 0.80f, 0.85f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.88f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.95f);
                    }
                    // CDJ Jog Platter & Ring
                    else if (matName.Contains("jogring"))
                    {
                        SetMatColor(mat, new Color(0.06f, 0.07f, 0.09f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.88f);
                        SetMatEmission(mat, new Color(0.0f, 0.45f, 1.0f) * 0.65f);
                    }
                    // Slot LED & Illuminated Pads
                    else if (matName.Contains("cdj_slot_led") || matName.Contains("slot_led"))
                    {
                        Color slotBlue = new Color(0.0f, 0.55f, 1.0f);
                        SetMatColor(mat, slotBlue);
                        SetMatEmission(mat, slotBlue * 1.8f);
                    }
                    // Play & Cue Buttons
                    else if (matName.Contains("btn_play"))
                    {
                        Color playGreen = new Color(0.0f, 0.90f, 0.40f);
                        SetMatColor(mat, playGreen);
                        SetMatEmission(mat, playGreen * 1.8f);
                    }
                    else if (matName.Contains("btn_cue"))
                    {
                        Color cueOrange = new Color(1.0f, 0.35f, 0.05f);
                        SetMatColor(mat, cueOrange);
                        SetMatEmission(mat, cueOrange * 1.8f);
                    }
                    // Studio Monitor Kevlar Cone (Non-emissive so it never glows in the dark!)
                    else if (matName.Contains("monitor_kevlar") || matName.Contains("kevlar"))
                    {
                        SetMatColor(mat, new Color(0.88f, 0.68f, 0.12f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.10f);
                        mat.DisableKeyword("_EMISSION");
                        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                    }
                    // Funktion-One Violet Axehead
                    else if (matName.Contains("axehead"))
                    {
                        SetMatColor(mat, new Color(0.22f, 0.06f, 0.32f, 1f));
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.68f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.45f);
                    }
                    // Speaker Cabinets & Cables
                    else if (matName.Contains("speaker") || matName.Contains("driver"))
                    {
                        SetMatColor(mat, new Color(0.025f, 0.025f, 0.030f, 1f));
                        if (speakerGrilleTex != null && mat.HasProperty("_BumpMap"))
                        {
                            mat.SetTexture("_BumpMap", speakerGrilleTex);
                            mat.EnableKeyword("_NORMALMAP");
                            mat.SetTextureScale("_BumpMap", new Vector2(3f, 3f));
                        }
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.50f);
                    }
                    // Wall Neon Strips
                    else if (matName.Contains("neon_blue"))
                    {
                        Color blueCol = new Color(0.0f, 0.45f, 1.0f);
                        SetMatColor(mat, blueCol);
                        SetMatEmission(mat, blueCol * 14.0f);
                    }
                    else if (matName.Contains("neon_red"))
                    {
                        Color redCol = new Color(1.0f, 0.02f, 0.10f);
                        SetMatColor(mat, redCol);
                        SetMatEmission(mat, redCol * 15.0f);
                    }
                }

                if (matsUpdated)
                {
                    mr.sharedMaterials = sharedMats;
                }
            }

            // 6. Create Mirror-Reflective Dancefloor Surface + Realtime Reflection Probe ("Der Boden soll reflektieren")
            GameObject floorMirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorMirrorQuad.name = "Floor_Reflective_Mirror";
            floorMirrorQuad.layer = 4; // Water layer (excluded from reflection camera so it doesn't occlude)
            floorMirrorQuad.transform.SetParent(worldInstance.transform);
            floorMirrorQuad.transform.position = new Vector3(0f, 0.005f, 0.5f);
            floorMirrorQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floorMirrorQuad.transform.localScale = new Vector3(12.6f, 16.0f, 1f);
            Object.DestroyImmediate(floorMirrorQuad.GetComponent<Collider>());

            MeshRenderer floorMirrorRenderer = floorMirrorQuad.GetComponent<MeshRenderer>();
            floorMirrorRenderer.sharedMaterial = floorReflMat;
            floorMirrorRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var planarRefl = floorMirrorQuad.AddComponent<GhostclubPlanarReflection>();
            planarRefl.floorReflectionMaterial = floorReflMat;
            planarRefl.floorHeight = 0.0f;
            planarRefl.reflectionIntensity = 0.65f;

            // Box-Projected Realtime Reflection Probe for PBR Specular Reflections on all surfaces
            GameObject probeGo = new GameObject("Club_Realtime_ReflectionProbe");
            probeGo.transform.SetParent(worldInstance.transform);
            probeGo.transform.position = new Vector3(0f, 2.5f, 0f);
            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = new Vector3(14.0f, 6.2f, 20.0f);
            probe.resolution = 256;
            probe.intensity = 1.4f;
            probe.cullingMask = ~(1 << 4);

            // 7. Create Dedicated 16:9 Widescreen Video Wall ("Leinwand") Directly Behind DJ Booth
            GameObject videoWallQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoWallQuad.name = "VJ_Widescreen_Leinwand";
            videoWallQuad.transform.SetParent(worldInstance.transform);
            videoWallQuad.transform.position = new Vector3(0f, 3.55f, -8.82f);
            videoWallQuad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            videoWallQuad.transform.localScale = new Vector3(7.55f, 4.18f, 1f);

            MeshRenderer vjScreenRenderer = videoWallQuad.GetComponent<MeshRenderer>();
            vjScreenRenderer.sharedMaterial = videoWallMat;
            vjScreenRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var vjComp = videoWallQuad.AddComponent<GhostclubVJScreen>();
            vjComp.targetRenderer = vjScreenRenderer;

            var streamPlayer = videoWallQuad.AddComponent<GhostclubStreamPlayer>();
            streamPlayer.vjScreenRenderer = vjScreenRenderer;

            // 8. Hang Individual Party Posters Flush on the Concrete Club Walls (Never Over Lights!)
            BuildClubPartyPosters(worldInstance.transform, matDir, urpLit);

            // 9. Setup VRSL DMX Lighting Director + AudioLink + Wall Neon + Fog + Video Mood Lighting
            GameObject lightingManagerGo = new GameObject("VRSL_DMX_Lighting_Director");
            GhostclubAudioLink audioLink = lightingManagerGo.AddComponent<GhostclubAudioLink>();
            GhostclubLightingSystem lightSystem = lightingManagerGo.AddComponent<GhostclubLightingSystem>();
            GhostclubWallNeonSystem wallNeonSystem = lightingManagerGo.AddComponent<GhostclubWallNeonSystem>();
            GhostclubFogSystem fogSystem = lightingManagerGo.AddComponent<GhostclubFogSystem>();
            GhostclubVideoMoodLighting videoMood = lightingManagerGo.AddComponent<GhostclubVideoMoodLighting>();
            GhostclubVRSLControlPanel controlPanel = lightingManagerGo.AddComponent<GhostclubVRSLControlPanel>();

            // Create 5 Video-Driven Club Mood Wash Lights ("Das Video soll dann die Lichtstimmung erzeugen")
            wallNeonSystem.lightingSystem = lightSystem;
            videoMood.videoWallRenderer = vjScreenRenderer;
            videoMood.vjScreen = vjComp;
            videoMood.lightingSystem = lightSystem;
            videoMood.wallNeonSystem = wallNeonSystem;
            videoMood.clubReflectionProbe = probe;
            videoMood.leftStageWash = CreateMoodLight(lightingManagerGo.transform, "VideoMood_LeftStage", new Vector3(-3.2f, 3.4f, -7.8f), Quaternion.Euler(18f, 18f, 0f), LightType.Spot, 115f, 18f);
            videoMood.centerRoomWash = CreateMoodLight(lightingManagerGo.transform, "VideoMood_CenterRoom", new Vector3(0f, 3.6f, -8.4f), Quaternion.Euler(14f, 0f, 0f), LightType.Spot, 130f, 22f);
            videoMood.rightStageWash = CreateMoodLight(lightingManagerGo.transform, "VideoMood_RightStage", new Vector3(3.2f, 3.4f, -7.8f), Quaternion.Euler(18f, -18f, 0f), LightType.Spot, 115f, 18f);
            videoMood.frontFloorBounce = CreateMoodLight(lightingManagerGo.transform, "VideoMood_FrontFloor", new Vector3(0f, 2.2f, -2.5f), Quaternion.identity, LightType.Point, 0f, 14f);
            videoMood.rearFloorBounce = CreateMoodLight(lightingManagerGo.transform, "VideoMood_RearFloor", new Vector3(0f, 2.4f, 3.5f), Quaternion.identity, LightType.Point, 0f, 14f);

            controlPanel.lightingSystem = lightSystem;
            controlPanel.videoMoodLighting = videoMood;
            controlPanel.wallNeonSystem = wallNeonSystem;
            controlPanel.fogSystem = fogSystem;
            controlPanel.audioLink = audioLink;
            controlPanel.streamPlayer = streamPlayer;

            // 10. Instantiate Room-Distributed VRSL Trusses & 10 VRSL Moving Heads Across the Whole Club!
            BuildVRSLMovingHeadsAndTrusses(
                lightingManagerGo.transform,
                lightSystem,
                moverBodyMat,
                moverLensBlueMat,
                moverLensRedMat,
                sharedBeamMat
            );

            // 11. Dedicated DJ Booth Overhead Key Light (Illuminates the 1x CDJ-3000, DJM-A9, Laptop & Monitors when stage lights are ON; 0 when lights are OFF!)
            for (int k = 0; k < 2; k++)
            {
                float kx = (k == 0) ? -0.35f : 0.45f;
                GameObject boothLightGo = new GameObject($"DJ_Booth_KeyLight_{k}");
                boothLightGo.transform.SetParent(lightingManagerGo.transform);
                boothLightGo.transform.position = new Vector3(kx, 2.85f, -6.50f);
                boothLightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                Light bl = boothLightGo.AddComponent<Light>();
                bl.type = LightType.Spot;
                bl.spotAngle = 62f;
                bl.innerSpotAngle = 38f;
                bl.range = 4.5f;
                bl.intensity = 3.8f;
                bl.color = (k == 0) ? new Color(0.15f, 0.55f, 1.0f) : new Color(1.0f, 0.12f, 0.25f);
                bl.shadows = LightShadows.None;

                lightSystem.djBoothLights.Add(bl);
            }

            // 12. Stage Laser Scanner Array
            GameObject laserArrayGo = new GameObject("VRSL_Stage_LaserScanner");
            laserArrayGo.transform.SetParent(lightingManagerGo.transform);
            laserArrayGo.transform.position = new Vector3(0f, 5.05f, -5.2f);
            laserArrayGo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

            GhostclubLaserScanner laserScanner = laserArrayGo.AddComponent<GhostclubLaserScanner>();
            laserScanner.beamCount = 11;
            laserScanner.fanSpreadAngle = 58f;
            laserScanner.beamLength = 24f;
            laserScanner.laserColor = new Color(0.0f, 0.55f, 1.0f);
            laserScanner.sharedLaserMaterial = sharedLaserMat;
            lightSystem.stageLaserScanner = laserScanner;

            // 13. Strobe Point Lights (Blood Red & Cyber Blue)
            for (int s = 0; s < 3; s++)
            {
                GameObject strobeGo = new GameObject($"Strobe_Light_{s}");
                strobeGo.transform.SetParent(lightingManagerGo.transform);
                strobeGo.transform.position = new Vector3(0f, 5.2f, -4.5f + (s * 4.5f));

                Light strobe = strobeGo.AddComponent<Light>();
                strobe.type = LightType.Point;
                strobe.range = 18f;
                strobe.intensity = 0f;
                strobe.color = (s % 2 == 0) ? new Color(1.0f, 0.02f, 0.10f) : new Color(0.0f, 0.45f, 1.0f);

                lightSystem.strobeLights.Add(strobe);
            }

            // 14. Setup Post-Processing Global Volume (URP)
            GameObject volumeGo = new GameObject("Global_PostProcessing_Volume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;

            string profilePath = "Assets/GhostclubWorld/Settings/Ghostclub_VolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile != null)
            {
                volume.sharedProfile = profile;
            }

            // 15. RenderSettings (Ambient Light strictly Color.black so posters/walls don't glow, with subtle dark-room reflections!)
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = Color.black;

            // 16. Setup Player Character (Spawned on the 80-Avatar Reflective Dancefloor facing the Stage!)
            GameObject playerGo = new GameObject("Ghostclub_Player");
            playerGo.transform.position = new Vector3(0f, 0.08f, 4.8f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Facing Stage (-Z)

            CharacterController cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(playerGo.transform);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camGo.transform.localRotation = Quaternion.identity;

            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.fieldOfView = 76f;
            camGo.AddComponent<AudioListener>();

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
            }

            GhostclubPlayerController controller = playerGo.AddComponent<GhostclubPlayerController>();
            controller.cameraTransform = camGo.transform;

            playerGo.AddComponent<AudioSource>();
            playerGo.AddComponent<GhostclubAudioDrone>();

            // 17. Save Scene
            string sceneDir = "Assets/GhostclubWorld/Scenes";
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            string scenePath = $"{sceneDir}/Ghostclub_5_0.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Ghostclub] SUCCESS! 80-Avatar VRChat Ghostclub 5.0 built and saved to: {scenePath}");
        }

        static Light CreateMoodLight(Transform parent, string name, Vector3 pos, Quaternion rot, LightType type, float spotAngle, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = rot;
            Light l = go.AddComponent<Light>();
            l.type = type;
            if (type == LightType.Spot)
            {
                l.spotAngle = spotAngle;
                l.innerSpotAngle = spotAngle * 0.65f;
            }
            l.range = range;
            l.intensity = 4.0f;
            l.shadows = LightShadows.None;
            return l;
        }

        static void BuildVRSLMovingHeadsAndTrusses(
            Transform parent,
            GhostclubLightingSystem lightSystem,
            Material moverBodyMat,
            Material lensBlueMat,
            Material lensRedMat,
            Material sharedBeamMat)
        {
            GameObject vrslTrussPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GhostclubWorld/Models/VRSL/Truss - 3.003.fbx");
            GameObject vrslMoverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GhostclubWorld/Models/VRSL/MoverLight--RTP-SplitMesh-SingleSided-HQ.fbx");

            // 1. Instantiate Detailed VRSL Cross-Trusses at Stage (Z = -5.2), Mid Dancefloor (Z = -1.6), and Front Dancefloor (Z = +1.5)
            float[] crossZs = new float[] { -5.2f, -1.6f, 1.5f };
            if (vrslTrussPrefab != null)
            {
                for (int t = 0; t < crossZs.Length; t++)
                {
                    GameObject crossTruss = (GameObject)PrefabUtility.InstantiatePrefab(vrslTrussPrefab, parent);
                    crossTruss.name = $"VRSL_Cross_Truss_{t}";
                    crossTruss.transform.position = new Vector3(4.2f, 4.75f, crossZs[t]);
                    crossTruss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    crossTruss.transform.localScale = new Vector3(1.1f, 0.9f, 0.9f);
                    ApplyMaterialToHierarchy(crossTruss, moverBodyMat);
                }

                // Left & Right Longitudinal Dancefloor Trusses
                for (int side = 0; side < 2; side++)
                {
                    float sx = (side == 0) ? -3.8f : 3.8f;
                    GameObject sideTruss = (GameObject)PrefabUtility.InstantiatePrefab(vrslTrussPrefab, parent);
                    sideTruss.name = $"VRSL_Longitudinal_Truss_{(side == 0 ? "Left" : "Right")}";
                    sideTruss.transform.position = new Vector3(sx, 4.80f, 4.0f);
                    sideTruss.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    sideTruss.transform.localScale = new Vector3(1.4f, 0.9f, 0.9f);
                    ApplyMaterialToHierarchy(sideTruss, moverBodyMat);
                }
            }

            // 2. 10 VRSL Moving Heads Distributed Across the Entire 80-Avatar Club Room:
            // - 4 on Stage Cross-Truss (Z = -5.2)
            // - 3 on Mid-Club Cross-Truss (Z = -1.6)
            // - 3 on Front Dancefloor Truss (Z = +1.5)
            Vector3[] moverPositions = new Vector3[]
            {
                // Stage Truss (4 Fixtures)
                new Vector3(-3.4f, 4.60f, -5.2f),
                new Vector3(-1.2f, 4.60f, -5.2f),
                new Vector3( 1.2f, 4.60f, -5.2f),
                new Vector3( 3.4f, 4.60f, -5.2f),

                // Mid-Club Dancefloor Truss (3 Fixtures)
                new Vector3(-3.6f, 4.55f, -1.6f),
                new Vector3( 0.0f, 4.55f, -1.6f),
                new Vector3( 3.6f, 4.55f, -1.6f),

                // Front Dancefloor Truss (3 Fixtures)
                new Vector3(-3.2f, 4.50f,  1.5f),
                new Vector3( 0.0f, 4.50f,  1.5f),
                new Vector3( 3.2f, 4.50f,  1.5f)
            };

            Color blueCol = new Color(0.0f, 0.50f, 1.0f);
            Color redCol = new Color(1.0f, 0.02f, 0.10f);

            for (int i = 0; i < moverPositions.Length; i++)
            {
                Vector3 pos = moverPositions[i];
                bool isBlue = (i % 2 == 0);
                Color beamCol = isBlue ? blueCol : redCol;
                Material lensMat = isBlue ? lensBlueMat : lensRedMat;

                GameObject moverGo = new GameObject($"VRSL_MovingHead_{i:00}");
                moverGo.transform.SetParent(parent);
                moverGo.transform.position = pos;
                // Rear fixtures face -Z toward dancefloor/stage; front/mid fixtures face +Z toward dancefloor
                moverGo.transform.rotation = (pos.z > 2.0f) ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

                GameObject clampGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                clampGo.name = "Mover_Base_Housing";
                clampGo.transform.SetParent(moverGo.transform, false);
                clampGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                clampGo.transform.localScale = new Vector3(0.38f, 0.14f, 0.38f);
                Object.DestroyImmediate(clampGo.GetComponent<Collider>());
                clampGo.GetComponent<MeshRenderer>().sharedMaterial = moverBodyMat;

                GameObject yokeGo = new GameObject("Yoke_Pivot");
                yokeGo.transform.SetParent(moverGo.transform, false);
                yokeGo.transform.localPosition = Vector3.zero;
                yokeGo.transform.localRotation = Quaternion.identity;

                for (int arm = -1; arm <= 1; arm += 2)
                {
                    GameObject armGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    armGo.name = $"Yoke_Arm_{(arm < 0 ? "L" : "R")}";
                    armGo.transform.SetParent(yokeGo.transform, false);
                    armGo.transform.localPosition = new Vector3(arm * 0.18f, -0.16f, 0f);
                    armGo.transform.localScale = new Vector3(0.05f, 0.36f, 0.08f);
                    Object.DestroyImmediate(armGo.GetComponent<Collider>());
                    armGo.GetComponent<MeshRenderer>().sharedMaterial = moverBodyMat;
                }

                if (vrslMoverPrefab != null)
                {
                    GameObject vrslInst = (GameObject)PrefabUtility.InstantiatePrefab(vrslMoverPrefab, yokeGo.transform);
                    vrslInst.name = "VRSL_HQ_Mesh";
                    vrslInst.transform.localPosition = new Vector3(0f, -0.16f, 0f);
                    vrslInst.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
                    foreach (Renderer r in vrslInst.GetComponentsInChildren<Renderer>())
                    {
                        string rName = r.gameObject.name.ToLower();
                        if (rName.Contains("volumetric") || rName.Contains("projection") || rName.Contains("base"))
                        {
                            r.enabled = false;
                        }
                        else
                        {
                            r.sharedMaterial = moverBodyMat;
                        }
                    }
                }

                GameObject headGo = new GameObject("Head_Pivot");
                headGo.transform.SetParent(yokeGo.transform, false);
                headGo.transform.localPosition = new Vector3(0f, -0.28f, 0f);
                headGo.transform.localRotation = Quaternion.identity;

                GameObject barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrelGo.name = "Mover_Head_Barrel";
                barrelGo.transform.SetParent(headGo.transform, false);
                barrelGo.transform.localPosition = new Vector3(0f, 0f, 0.04f);
                barrelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                barrelGo.transform.localScale = new Vector3(0.26f, 0.21f, 0.26f);
                Object.DestroyImmediate(barrelGo.GetComponent<Collider>());
                barrelGo.GetComponent<MeshRenderer>().sharedMaterial = moverBodyMat;

                GameObject lensGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lensGo.name = "Mover_Glowing_Lens";
                lensGo.transform.SetParent(headGo.transform, false);
                lensGo.transform.localPosition = new Vector3(0f, 0f, 0.245f);
                lensGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                lensGo.transform.localScale = new Vector3(0.22f, 0.015f, 0.22f);
                Object.DestroyImmediate(lensGo.GetComponent<Collider>());
                MeshRenderer lensRenderer = lensGo.GetComponent<MeshRenderer>();
                lensRenderer.sharedMaterial = lensMat;

                GameObject rimLightGo = new GameObject("Fixture_RimLight");
                rimLightGo.transform.SetParent(moverGo.transform, false);
                rimLightGo.transform.localPosition = new Vector3(0f, -0.22f, 0.22f);
                Light rimLight = rimLightGo.AddComponent<Light>();
                rimLight.type = LightType.Point;
                rimLight.range = 2.2f;
                rimLight.intensity = 2.0f;
                rimLight.color = beamCol;
                rimLight.shadows = LightShadows.None;

                GameObject spotGo = new GameObject("Spot");
                spotGo.transform.SetParent(headGo.transform, false);
                spotGo.transform.localPosition = new Vector3(0f, 0f, 0.25f);
                spotGo.transform.localRotation = Quaternion.identity;

                Light spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 30f;
                spot.innerSpotAngle = 15f;
                spot.range = 24f;
                spot.intensity = 6.5f;
                spot.color = beamCol;
                spot.shadows = (i < 4) ? LightShadows.Soft : LightShadows.None;

                var beam = spotGo.AddComponent<GhostclubVolumetricBeam>();
                beam.sharedBeamMaterial = sharedBeamMat;
                beam.beamAlphaMultiplier = 0.22f;

                GhostclubMovingHead mh = moverGo.AddComponent<GhostclubMovingHead>();
                mh.yokeTransform = yokeGo.transform;
                mh.headTransform = headGo.transform;
                mh.lensRenderer = lensRenderer;
                mh.spotLight = spot;
                mh.rimLight = rimLight;
                mh.volumetricBeam = beam;
                mh.beamColor = beamCol;
                mh.pan = (pos.x < 0 ? 25f : -25f);
                mh.tilt = 48f;

                lightSystem.movingHeads.Add(mh);
            }
        }

        static void BuildClubPartyPosters(Transform parent, string matDir, Shader urpLit)
        {
            GameObject postersRoot = new GameObject("Ghostclub_Party_Posters");
            postersRoot.transform.SetParent(parent);

            Material frameMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_Poster_Frame.mat", urpLit);
            SetMatColor(frameMat, new Color(0.03f, 0.035f, 0.045f, 1f));
            if (frameMat.HasProperty("_Metallic")) frameMat.SetFloat("_Metallic", 0.90f);
            if (frameMat.HasProperty("_Smoothness")) frameMat.SetFloat("_Smoothness", 0.70f);
            frameMat.DisableKeyword("_EMISSION");
            if (frameMat.HasProperty("_EmissionColor")) frameMat.SetColor("_EmissionColor", Color.black);

            // Non-Emissive Lit Poster Materials ("Die Poster sollen nicht leuchten sondern nur vom Licht der Lighting Systems angestrahlt werden")
            Material[] posterMats = new Material[6];
            for (int i = 0; i < 6; i++)
            {
                Texture2D pTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/GhostclubWorld/Textures/Poster_Party_0{i + 1}.png");
                Material pMat = CreateOrUpdateMaterialAsset($"{matDir}/Mat_Poster_0{i + 1}.mat", urpLit);
                SetMatColor(pMat, Color.white);
                SetMatTexture(pMat, pTex);
                if (pMat.HasProperty("_Smoothness")) pMat.SetFloat("_Smoothness", 0.25f);
                if (pMat.HasProperty("_Metallic")) pMat.SetFloat("_Metallic", 0.0f);
                if (pMat.HasProperty("_Cull")) pMat.SetFloat("_Cull", 0f);
                // Strictly NO self-emission on posters!
                pMat.DisableKeyword("_EMISSION");
                if (pMat.HasProperty("_EmissionColor")) pMat.SetColor("_EmissionColor", Color.black);
                if (pMat.HasProperty("_EmissionMap")) pMat.SetTexture("_EmissionMap", null);
                EditorUtility.SetDirty(pMat);
                posterMats[i] = pMat;
            }

            // All Posters are mounted FLUSH on the solid Concrete Side Walls in the open wall bays BETWEEN the Pillars!
            // Pillars (and their vertical Neon Lights) are strictly at Z = -6.0, -1.0, +4.2.
            // Open Concrete Wall Bays are at Z = -3.6 (Front Bay), Z = +1.5 (Mid Bay), and Z = +6.6 (Rear Bay)!
            // Each poster has its own individual design, color palette, and aspect ratio!

            // 1. Left Concrete Wall (X = -6.23m, facing +X into the club — flush on the wall in the clear bays between pillars!)
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_LeftWall_Bay1_Swiss",   new Vector3(-6.23f, 2.50f, -3.6f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.55f, 2.20f), posterMats[0], frameMat);
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_LeftWall_Bay2_Pulsar",  new Vector3(-6.23f, 2.45f,  1.5f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.65f, 2.30f), posterMats[4], frameMat);
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_LeftWall_Bay3_Vinyl",   new Vector3(-6.23f, 2.55f,  6.6f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.85f, 1.90f), posterMats[3], frameMat);

            // 2. Right Concrete Wall (X = +6.23m, facing -X into the club — flush on the wall in the clear bays between pillars!)
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_RightWall_Bay1_Acid",   new Vector3( 6.23f, 2.55f, -3.6f), Quaternion.Euler(0f,  90f, 0f), new Vector2(1.60f, 2.25f), posterMats[1], frameMat);
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_RightWall_Bay2_Tokyo",  new Vector3( 6.23f, 2.45f,  1.5f), Quaternion.Euler(0f,  90f, 0f), new Vector2(1.65f, 2.30f), posterMats[2], frameMat);
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_RightWall_Bay3_Stahl",  new Vector3( 6.23f, 2.50f,  6.6f), Quaternion.Euler(0f,  90f, 0f), new Vector2(1.50f, 2.15f), posterMats[5], frameMat);

            // 3. Entrance Back Concrete Wall (Z = +9.24m, facing -Z toward stage at X = -3.2m and X = +3.2m)
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_EntranceWall_Left",     new Vector3(-3.2f, 2.60f, 9.24f), Quaternion.Euler(0f, 0f, 0f), new Vector2(1.60f, 2.20f), posterMats[1], frameMat);
            PlacePosterFlushOnWall(postersRoot.transform, "Poster_EntranceWall_Right",    new Vector3( 3.2f, 2.60f, 9.24f), Quaternion.Euler(0f, 0f, 0f), new Vector2(1.60f, 2.20f), posterMats[3], frameMat);
        }

        static void PlacePosterFlushOnWall(Transform parent, string name, Vector3 pos, Quaternion rot, Vector2 size, Material posterMat, Material frameMat)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = rot;

            // Slim Wall-Mounted Backing Frame (flush against concrete wall, 2cm thick)
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Poster_Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(size.x + 0.08f, size.y + 0.08f, 0.02f);
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

            // Front Poster Print (Z = -0.015f, facing -Z)
            GameObject quadFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadFront.name = "Poster_Print_Front";
            quadFront.transform.SetParent(root.transform, false);
            quadFront.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            quadFront.transform.localRotation = Quaternion.identity;
            quadFront.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(quadFront.GetComponent<Collider>());
            MeshRenderer mrFront = quadFront.GetComponent<MeshRenderer>();
            mrFront.sharedMaterial = posterMat;
            mrFront.shadowCastingMode = ShadowCastingMode.Off;

            // Back Poster Print (Z = +0.015f, rotated 180° around Y)
            GameObject quadBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadBack.name = "Poster_Print_Back";
            quadBack.transform.SetParent(root.transform, false);
            quadBack.transform.localPosition = new Vector3(0f, 0f, 0.015f);
            quadBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quadBack.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(quadBack.GetComponent<Collider>());
            MeshRenderer mrBack = quadBack.GetComponent<MeshRenderer>();
            mrBack.sharedMaterial = posterMat;
            mrBack.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material CreateOrUpdateMaterialAsset(string assetPath, Shader shader)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetMatColor(Material mat, Color col)
        {
            if (mat == null) return;
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            EditorUtility.SetDirty(mat);
        }

        static void SetMatTexture(Material mat, Texture2D tex)
        {
            if (mat == null || tex == null) return;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
        }

        static void SetMatEmission(Material mat, Color hdrColor, Texture2D emissTex = null)
        {
            if (mat == null) return;
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (emissTex != null && mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emissTex);
            }
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", hdrColor);
            }
            mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
        }

        static void ApplyMaterialToHierarchy(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
            }
        }
    }
}
