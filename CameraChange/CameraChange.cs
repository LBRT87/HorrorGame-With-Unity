using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class CameraChanger : NetworkBehaviour
{
    public CinemachineCamera ThirdPersonCamera;
    public CinemachineCamera FirstPersonCamera;
    public bool isFirstPerson = false;


    public Camera playerCamera; 

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                if (playerCamera.TryGetComponent<CinemachineBrain>(out var brain))
                    brain.enabled = false;
            }

            ThirdPersonCamera.gameObject.SetActive(false);
            FirstPersonCamera.gameObject.SetActive(false);
            return;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";

            var brain = playerCamera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = true;
        }

        InitCamera();
    }

    public void InitCamera()
    {
        ApplyCamera();
        if (ThirdPersonCamera != null) ThirdPersonCamera.Priority = 10;
        if (FirstPersonCamera != null) FirstPersonCamera.Priority = 10;
    }

    private void ApplyCamera()
    {
        if (ThirdPersonCamera != null)
            ThirdPersonCamera.gameObject.SetActive(!isFirstPerson);
        if (FirstPersonCamera != null)
            FirstPersonCamera.gameObject.SetActive(isFirstPerson);
    }

    public void gantiMode()
    {
        isFirstPerson = !isFirstPerson;
        ApplyCamera();
    }

    public Transform GetActiveCameraTransform()
    {
        return playerCamera != null ? playerCamera.transform : Camera.main.transform;
    }

    public void UpdateAllCamerasFOV(float fovValue)
    {
        if (ThirdPersonCamera != null) ThirdPersonCamera.Lens.FieldOfView = fovValue;
        if (FirstPersonCamera != null) FirstPersonCamera.Lens.FieldOfView = fovValue;
    }
}