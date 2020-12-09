// using UnityEngine;
// using System;
// using System.Runtime.InteropServices;
// using UnityEngine.UI;
//
// #if ENABLE_WINMD_SUPPORT
// using HL2UnityPlugin;
// // #endif
//
// public class SensorTest : MonoBehaviour
// {
// #if ENABLE_WINMD_SUPPORT
//     HL2ResearchMode researchMode;
// #endif
//
//     [SerializeField]
//     GameObject previewPlane = null;
//     [SerializeField]
//     Text text;
//     private Material mediaMaterial = null;
//     private Texture2D mediaTexture = null;
//     private byte[] frameData = null;
//
//     void Start()
//     {
// #if ENABLE_WINMD_SUPPORT
//         researchMode = new HL2ResearchMode();
//         researchMode.InitializeDepthSensor();
// #endif
//         mediaMaterial = previewPlane.GetComponent<MeshRenderer>().material;
//     }
//
//     #region Button Events
//     public void PrintDepthEvent()
//     {
// #if ENABLE_WINMD_SUPPORT
//         text.text = researchMode.GetCenterDepth().ToString();
// #endif
//     }
//
//     public void PrintDepthExtrinsicsEvent()
//     {
// #if ENABLE_WINMD_SUPPORT
//         text.text = researchMode.PrintDepthExtrinsics();
// #endif
//     }
//
//     public void StartDepthSensingLoopEvent()
//     {
// #if ENABLE_WINMD_SUPPORT
//         researchMode.StartDepthSensorLoop();
// #endif
//     }
//
//     public void StopSensorLoopEvent()
//     {
// #if ENABLE_WINMD_SUPPORT
//         researchMode.StopAllSensorDevice();
// #endif
//     }
//
//     bool startRealtimePreview = false;
//     public void StartPreviewEvent()
//     {
//         startRealtimePreview = !startRealtimePreview;
//     }
//     #endregion
//
//     private void LateUpdate()
//     {
// #if ENABLE_WINMD_SUPPORT
//         PrintDepthEvent();
//
//         // update depth map texture
//         if (startRealtimePreview && researchMode.DepthMapTextureUpdated())
//         {
//             if (!mediaTexture)
//             {
//                 mediaTexture = new Texture2D(512, 512, TextureFormat.Alpha8, false);
//                 mediaMaterial.mainTexture = mediaTexture;
//             }
//
//             byte[] frameTexture = researchMode.GetDepthMapTextureBuffer();
//             if (frameTexture.Length > 0)
//             {
//                 if (frameData == null)
//                 {
//                     frameData = frameTexture;
//                 }
//                 else
//                 {
//                     System.Buffer.BlockCopy(frameTexture, 0, frameData, 0, frameData.Length);
//                 }
//
//                 if (frameData != null)
//                 {
//                     mediaTexture.LoadRawTextureData(frameData);
//                     mediaTexture.Apply();
//                 }
//             }
//         }
// #endif
//     }
// }