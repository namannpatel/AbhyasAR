using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shared touch/mouse-fallback helpers for every script in this module that raycasts
/// screen taps against world content (AR plane placement, hazard/call-point/extinguisher
/// interaction). Centralized here so there's exactly one tap-detection implementation
/// instead of several drifting copies, and so the UI-pointer guard below only needs to be
/// added once: without it, tapping the on-screen instructions/HUD/Retry button also
/// registers as a world tap and can misfire whichever stage is currently listening.
/// </summary>
public static class ArTouchInput
{
    /// <summary>True on the frame a touch/click begins, with its screen position — false if the tap started over UI.</summary>
    public static bool TryGetTapPosition(out Vector2 screenPos)
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPos = Input.GetTouch(0).position;
            return !IsOverUI(Input.GetTouch(0).fingerId);
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return !IsOverUI();
        }

        screenPos = default;
        return false;
    }

    /// <summary>True on every frame a touch/click is currently down, with its screen position — false while held over UI.</summary>
    public static bool TryGetHeldPosition(out Vector2 screenPos)
    {
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                screenPos = t.position;
                return !IsOverUI(t.fingerId);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            return !IsOverUI();
        }

        screenPos = default;
        return false;
    }

    private static bool IsOverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
