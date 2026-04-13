using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class OuijaGameIntro : MonoBehaviour
{
    [SerializeField] private GameObject ouijaBoardObject;
    [SerializeField] private Transform ouijaSpawnPoint;
    [SerializeField] private float introDuration = 5f;


    public void PlayIntroIfActive(Action onDone)
    {
        if (!OuijaSettings.IsActive)
        {
            onDone?.Invoke();
            return;
        }

        StartCoroutine(IntroRoutine(onDone));
    }

    public Transform GetSpawnPoint() => ouijaSpawnPoint;

    private IEnumerator IntroRoutine(Action onDone)
    {
        if (ouijaBoardObject != null)
            ouijaBoardObject.SetActive(true);

        SetAllInputActive(false);

        var cameras = new List<(Camera cam, Quaternion originalRot)>();
        foreach (var cc in FindObjectsByType<CameraChanger>(FindObjectsSortMode.None))
        {
            var cam = cc.GetActiveCameraTransform()?.GetComponent<Camera>();
            if (cam == null) continue;

            cameras.Add((cam, cam.transform.rotation));

            if (ouijaSpawnPoint != null)
            {
                Vector3 dir = ouijaSpawnPoint.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    cam.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        yield return new WaitForSeconds(introDuration);

        foreach (var (cam, originalRot) in cameras)
            if (cam != null) cam.transform.rotation = originalRot;
        SetAllInputActive(true);

        onDone?.Invoke();
    }

    private void SetAllInputActive(bool active)
    {
        foreach (var input in FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None))
            if (input.IsOwner) input.enabled = active;
        foreach (var ghost in FindObjectsByType<GhostInputHandler>(FindObjectsSortMode.None))
            if (ghost.IsOwner) ghost.enabled = active;
    }


}