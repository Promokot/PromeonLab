//
//  PromeonOutlineMask.shader
//  Fork of QuickOutline "Custom/Outline Mask" with a per-instance _StencilRef property.
//  Lives under _App/Content so a QuickOutline package reimport does not overwrite it.
//

Shader "PromeonLab/OutlineMask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _StencilRef("Stencil Ref", Float) = 1
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+100"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "Mask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref [_StencilRef]
        Pass Replace
      }
    }
  }
}
