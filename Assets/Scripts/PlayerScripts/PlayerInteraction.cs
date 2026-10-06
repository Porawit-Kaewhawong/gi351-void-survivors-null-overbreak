using UnityEngine;

public class PlayerLobbyInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 15f;
    public LayerMask interactableLayer = ~0;
    public bool useCenterCrosshair = true;
    public KeyCode interactKey = KeyCode.E;

    private Camera cam;
    private LobbySelection currentTarget;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        HandleHoverDetection();
        HandleInteractionInput();
    }

    private Ray GetInteractionRay()
    {
        if (cam == null) return new Ray();

        return useCenterCrosshair
            ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : cam.ScreenPointToRay(Input.mousePosition);
    }

    private void HandleHoverDetection()
    {
        Ray ray = GetInteractionRay();
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            LobbySelection pedestal = hit.collider.GetComponentInParent<LobbySelection>();

            if (pedestal != null)
            {
                if (currentTarget != pedestal)
                {
                    // Deactivate previous hover UI
                    if (currentTarget != null) currentTarget.SetHoverState(false);

                    // Activate new hover target UI
                    currentTarget = pedestal;
                    currentTarget.SetHoverState(true);
                }
                return;
            }
        }

        // Clear hover state if looking away or aiming at void
        if (currentTarget != null)
        {
            currentTarget.SetHoverState(false);
            currentTarget = null;
        }
    }

    private void HandleInteractionInput()
    {
        // Only trigger selection when pressing the specified interact key (E)
        if (currentTarget != null && Input.GetKeyDown(interactKey))
        {
            LobbySelection selected = currentTarget;

            // Clear hover before confirming selection
            currentTarget.SetHoverState(false);
            currentTarget = null;

            selected.SelectThisOption();
            Debug.Log($"Player Selected: {selected.data?.title}");
        }
    }
}