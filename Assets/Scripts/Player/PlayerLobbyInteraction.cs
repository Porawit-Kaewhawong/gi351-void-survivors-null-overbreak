using UnityEngine;

public class PlayerLobbyInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 15f;
    public LayerMask interactableLayer = ~0;
    public bool useCenterCrosshair = true;
    public KeyCode interactKey = KeyCode.E;

    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        Ray ray = GetInteractionRay();
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);

        if (Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0))
        {
            TryInteract(ray);
        }
    }

    private Ray GetInteractionRay()
    {
        if (cam == null) return new Ray();

        return useCenterCrosshair
            ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : cam.ScreenPointToRay(Input.mousePosition);
    }

    private void TryInteract(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            LobbySelection pedestal = hit.collider.GetComponentInParent<LobbySelection>();
            if (pedestal != null)
            {
                pedestal.SelectThisOption();
                Debug.Log($"Player Selected: {pedestal.data?.title}");
            }
        }
    }
}