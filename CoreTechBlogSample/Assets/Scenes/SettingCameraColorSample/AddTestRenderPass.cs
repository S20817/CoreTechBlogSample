using System;
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

        public void Setup(TestRenderPassSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_settings.material == null)
                return;

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

            // cameraColorを差し替える
            resourceData.cameraColor = tempTarget;
        }
    }
}
