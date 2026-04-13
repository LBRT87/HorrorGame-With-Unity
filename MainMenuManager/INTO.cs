using System.Collections;
using UnityEngine;

public class CinematicIntroController : MonoBehaviour
{
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private GameObject canvasHitam;  
    [SerializeField] private Transform[] pathPoints;  

    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float lookSpeed = 3f;
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup _fadeGroup;

    private void Start()
    {
        _fadeGroup = canvasHitam?.GetComponent<CanvasGroup>();
        cinematicCamera.gameObject.SetActive(true);
        canvasHitam?.SetActive(true);
        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        yield return StartCoroutine(Fade(1f, 0f));

        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            Transform from = pathPoints[i];
            Transform to = pathPoints[i + 1];
            float dist = Vector3.Distance(from.position, to.position);
            float duration = dist / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                cinematicCamera.transform.position = Vector3.Lerp(from.position, to.position, t);
                cinematicCamera.transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);

                yield return null;
            }
        }

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(Fade(0f, 1f));

        cinematicCamera.gameObject.SetActive(false);
        canvasHitam?.SetActive(false);

        var input = FindFirstObjectByType<PlayerInputHandler>();
        if (input != null)
        {
            input.enabled = true;

        }

        var ghostInput = FindFirstObjectByType<GhostInputHandler>();
        if (ghostInput != null) 
            ghostInput.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeGroup == null) yield break;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        _fadeGroup.alpha = to;
    }
}