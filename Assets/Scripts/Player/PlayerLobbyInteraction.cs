using UnityEngine;

public class PlayerLobbyInteraction : MonoBehaviour
{
    [Header("TPS Settings")]
    [Tooltip("Set this higher (e.g. 15-20) because the TPS camera sits far behind the player.")]
    public float interactDistance = 15f;

    [Tooltip("Select layers to interact with. Make sure to EXCLUDE the 'Player' layer!")]
    public LayerMask interactableLayer = ~0;

    [Tooltip("If TRUE, aims at screen center (Crosshair). If FALSE, aims at mouse cursor.")]
    public bool useCenterCrosshair = true;

    [Header("Input Keys")]
    public KeyCode interactKey = KeyCode.E;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        Ray ray = GetInteractionRay();

        // Draws a red line in Scene view so you can see where your camera is aiming
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);

        if (Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0))
        {
            TryInteract(ray);
        }
    }

    private Ray GetInteractionRay()
    {
        if (useCenterCrosshair)
        {
            // Ray from center of screen (Crosshair style)
            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            // Ray from free mouse cursor position
            return cam.ScreenPointToRay(Input.mousePosition);
        }
    }

    private void TryInteract(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            // Search for pedestal script on hit object or its parents
            LobbyOptionPedestal pedestal = hit.collider.GetComponentInParent<LobbyOptionPedestal>();

            if (pedestal != null)
            {
                pedestal.SelectThisOption();
                Debug.Log($"[TPS Interaction] Selected: {pedestal.data?.title}");
            }
            else
            {
                Debug.Log($"[TPS Interaction] Hit '{hit.collider.name}', but no LobbyOptionPedestal script was found on it.");
            }
        }
    }
}