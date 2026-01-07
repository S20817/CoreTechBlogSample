using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class AddTestRenderPass : MonoBehaviour
{
    [Serializable]
    public class TestRenderPassSettings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    [SerializeField]
    private TestRenderPassSettings settings;

    private Camera _cameraCache;

    private Camera CameraCache
    {
        get
        {
            if (_cameraCache == null)
                _cameraCache = GetComponent<Camera>();
            return _cameraCache;
        }
    }

    private UniversalAdditionalCameraData _cameraData;

    private UniversalAdditionalCameraData CameraData
    {
        get
        {
            if (_cameraData == null)
                _cameraData = GetComponent<UniversalAdditionalCameraData>();
            return _cameraData;
        }
    }

    private readonly TestRendererPass _testPass = new();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera inCamera)
    {
        if (inCamera != CameraCache)
            return;

        _testPass.Setup(settings);
        CameraData.scriptableRenderer.EnqueuePass(_testPass);
    }

    private class TestRendererPass : ScriptableRenderPass
    {
        private TestRenderPassSettings _settings;
        private List<Camera> _cameras;

        public void Setup(TestRenderPassSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
        }

        private bool IsLastCameraInStack(Camera camera)
        {
            var additionalCameraData = camera.GetUniversalAdditionalCameraData();
            
            // Base cameraの場合、スタックがなければ最後
            if (additionalCameraData.renderType == CameraRenderType.Base)
            {
                return additionalCameraData.cameraStack == null || additionalCameraData.cameraStack.Count == 0;
            }
            
            // Overlay cameraの場合、どのBase cameraのスタックに含まれているかを探す
            if (additionalCameraData.renderType == CameraRenderType.Overlay)
            {
                // すべてのBase cameraを検索してスタック内の位置を確認
                var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var cam in allCameras)
                {
                    var camData = cam.GetUniversalAdditionalCameraData();
                    if (camData.renderType == CameraRenderType.Base && camData.cameraStack != null)
                    {
                        int index = camData.cameraStack.IndexOf(camera);
                        if (index != -1)
                        {
                            // このカメラがスタックの最後かどうか
                            return index == camData.cameraStack.Count - 1;
                        }
                    }
                }
            }
            
            // デフォルトでは最後として扱う
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_settings.material == null)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            // バックバッファ直書きの場合は処理しない
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var activeCameraColor = resourceData.activeColorTexture;
            var tempDesc = renderGraph.GetTextureDesc(activeCameraColor);
            tempDesc.depthBufferBits = 0;
            tempDesc.msaaSamples = MSAASamples.None;
            tempDesc.name = "_MyEffect_Temp";
            tempDesc.clearBuffer = false;
            var tempTarget = renderGraph.CreateTexture(tempDesc);

            // 便利な関数でBlitPassを追加
            var blitParams = new RenderGraphUtils.BlitMaterialParameters(activeCameraColor, tempTarget, _settings.material, 0);
            renderGraph.AddBlitPass(blitParams, "Render My Effect");

            // ここでカメラがStack上の最後に描画されるかどうかを判定する
            bool isLastCamera = IsLastCameraInStack(cameraData.camera);
            
            if (isLastCamera)
            {
                // cameraColorを差し替える
                resourceData.cameraColor = tempTarget;
            }
            else
            {
                renderGraph.AddBlitPass(tempTarget, activeCameraColor, Vector2.one, Vector2.zero,
                    passName: "Blit Back to Camera Color");
            }
        }
    }
}
