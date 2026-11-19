using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light))]
public class LightCookieContinuo : MonoBehaviour
{
    [Header("Velocidade do Movimento")]
    public Vector2 velocidade = new Vector2(0.5f, 0.2f);

    private UniversalAdditionalLightData _lightData;
    private Vector2 _offsetAtual;

    private void Awake()
    {
        _lightData = GetComponent<UniversalAdditionalLightData>();
        if (_lightData != null)
        {
            _offsetAtual = _lightData.lightCookieOffset;
        }
    }

    private void Update()
    {
        if (_lightData == null) return;

        _offsetAtual += velocidade * Time.deltaTime;

        Vector2 tamanho = _lightData.lightCookieSize;
        if (tamanho.x > 0) _offsetAtual.x = Mathf.Repeat(_offsetAtual.x, tamanho.x);
        if (tamanho.y > 0) _offsetAtual.y = Mathf.Repeat(_offsetAtual.y, tamanho.y);

        _lightData.lightCookieOffset = _offsetAtual;
    }
}
