# CoreTechBlogSample
CoreTechブログのサンプルプロジェクト

## Unity Version
Unity 6000.2.2f1

## サンプルシーン

### SettingCameraColorSample
camraColor差し替えることで戻しBlitを回避できるサンプル

<img width="505" height="122" alt="image" src="https://github.com/user-attachments/assets/40d124bd-9987-470a-a5f2-212649b3fa95" />

Main Cameraにアタッチされている`Add Test Render Pass`から、`Rebder Pass Event`を`After Rendering Post Processing`以降に設定すると問題が発生することを確認できます
