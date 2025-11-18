using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PlayerStateController : MonoBehaviourPunCallbacks
{
    public enum PlayerState { NORMAL, MENU, CASTING, DEAD, TEXTING }

    public GameObject canvas;
    public GameObject cursor;

    private PlayerMovement playerMovement;
    public MouseLook mouseLook;
    public WeaponSwitcher weaponSwitcher;
    public SpellSlotSystem spellSlotSystem;
    public GameChat gameChat;

    private PlayerState currentState = PlayerState.NORMAL;
    private PlayerState previousState;

    // May be placed on DrawSpell or its child (CircleManager)
    private CircleUIAnimator circleAnimator;

    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        mouseLook = GetComponentInChildren<MouseLook>();
        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        spellSlotSystem = GetComponentInChildren<SpellSlotSystem>();

        EnsureCastingRefs();
    }

    private void EnsureCastingRefs()
    {
        if (spellSlotSystem == null) return;

        if (spellSlotSystem.SpellCastList == null)
        {
            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                if (tr.name.ToLower().Contains("drawspell"))
                {
                    spellSlotSystem.SpellCastList = tr.gameObject;
                    break;
                }
            }

            if (spellSlotSystem.SpellCastList == null)
                Debug.LogWarning("[PlayerStateController] SpellCastList is not assigned and DrawSpell not found. Hide animation will not work.");
        }

        if (spellSlotSystem.SpellCastList != null)
            circleAnimator = spellSlotSystem.SpellCastList.GetComponentInChildren<CircleUIAnimator>(true);

        if (circleAnimator == null && spellSlotSystem.SpellCastList != null)
        {
            Debug.LogWarning("[PlayerStateController] CircleUIAnimator not found under SpellCastList. Fallback to SetActive(true/false).");
        }
        else if (circleAnimator != null)
        {
            // Important: do not auto-play when canvas/object gets enabled
            circleAnimator.playOnEnable = false;
        }
    }

    public void SetGameChat(GameChat _gameChat) => gameChat = _gameChat;

    public void SetPlayerState(PlayerState newState)
    {
        if (currentState == newState) return;

        bool wasCasting = (currentState == PlayerState.CASTING);

        if (newState == PlayerState.TEXTING && currentState != PlayerState.TEXTING)
            previousState = currentState;
        if (newState == PlayerState.DEAD && currentState != PlayerState.DEAD)
            previousState = currentState;

        currentState = newState;

        switch (currentState)
        {
            case PlayerState.NORMAL:
                playerMovement.canMove = false;
                mouseLook.isCastingOrInMenu = false;
                gameChat.isInMenu = false;

                canvas.SetActive(true);
                cursor.SetActive(true);
                LockCursor();

                spellSlotSystem.isTextingOrInMenu = false;
                spellSlotSystem.isDrawing = false;

                if (wasCasting) HideCastingUI();
                weaponSwitcher.EnableSelectedWeapon(true);

                spellSlotSystem.CanCast = weaponSwitcher.isRingsActive;
                spellSlotSystem.UpdateSlotIcons();
                break;

            case PlayerState.CASTING:
                playerMovement.canMove = true;
                playerMovement.StopMovement();
                mouseLook.isCastingOrInMenu = true;
                gameChat.isInMenu = false;

                canvas.SetActive(true);
                cursor.SetActive(false);
                UnlockCursor();

                spellSlotSystem.isTextingOrInMenu = false;
                spellSlotSystem.isDrawing = true;

                if (!wasCasting) ShowCastingUI();
                weaponSwitcher.EnableSelectedWeapon(false);
                break;

            case PlayerState.MENU:
                playerMovement.canMove = true;
                playerMovement.StopMovement();
                mouseLook.isCastingOrInMenu = true;
                gameChat.isInMenu = true;

                canvas.SetActive(false);
                cursor.SetActive(false);
                UnlockCursor();

                spellSlotSystem.isTextingOrInMenu = true;

                if (wasCasting) HideCastingUI();
                weaponSwitcher.EnableSelectedWeapon(false);
                break;

            case PlayerState.TEXTING:
                playerMovement.canMove = true;
                playerMovement.StopMovement();
                mouseLook.isCastingOrInMenu = true;
                gameChat.isInMenu = false;

                canvas.SetActive(false);
                cursor.SetActive(false);
                UnlockCursor();

                spellSlotSystem.isTextingOrInMenu = true;
                if (wasCasting) HideCastingUI();
                weaponSwitcher.EnableSelectedWeapon(false);
                break;

            case PlayerState.DEAD:
                playerMovement.canMove = true;
                playerMovement.StopMovement();
                mouseLook.isCastingOrInMenu = true;
                gameChat.isInMenu = false;

                canvas.SetActive(false);
                cursor.SetActive(false);

                spellSlotSystem.isTextingOrInMenu = true;
                if (wasCasting) HideCastingUI();
                weaponSwitcher.EnableSelectedWeapon(false);
                break;
        }
    }

    public void ReturnToPreviousState() => SetPlayerState(previousState);

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public PlayerState GetCurrentState() => currentState;

    // ---------- Casting UI helpers ----------

    private void ShowCastingUI()
    {
        EnsureCastingRefs();
        if (spellSlotSystem == null || spellSlotSystem.SpellCastList == null) return;

        if (!spellSlotSystem.SpellCastList.activeSelf)
            spellSlotSystem.SpellCastList.SetActive(true);

        if (circleAnimator != null)
        {
            circleAnimator.playOnEnable = false;
            circleAnimator.deactivateOnHidden = (circleAnimator.gameObject == spellSlotSystem.SpellCastList);
            circleAnimator.Show();
            Debug.Log("[PlayerStateController] ShowCastingUI → CircleUIAnimator.Show()");
        }
        else
        {
            Debug.LogWarning("[PlayerStateController] ShowCastingUI: no CircleUIAnimator → just SetActive(true).");
        }
    }

    private void HideCastingUI()
    {
        EnsureCastingRefs();
        if (spellSlotSystem == null || spellSlotSystem.SpellCastList == null) return;

        if (!spellSlotSystem.SpellCastList.activeInHierarchy)
            return;
        if (circleAnimator != null && !circleAnimator.gameObject.activeInHierarchy)
            return;

        if (circleAnimator != null)
        {
            bool animatorOnRoot = (circleAnimator.gameObject == spellSlotSystem.SpellCastList);
            float wait = circleAnimator.hideDuration > 0f
                ? circleAnimator.hideDuration
                : circleAnimator.revealDuration;

            circleAnimator.deactivateOnHidden = animatorOnRoot;
            circleAnimator.Hide();
            Debug.Log("[PlayerStateController] HideCastingUI → CircleUIAnimator.Hide()");

            if (!animatorOnRoot)
                StartCoroutine(DisableAfter(wait, circleAnimator.useUnscaledTime));
        }
        else
        {
            Debug.LogWarning("[PlayerStateController] HideCastingUI: no CircleUIAnimator → SetActive(false).");
            spellSlotSystem.SpellCastList.SetActive(false);
        }
    }

    private IEnumerator DisableAfter(float seconds, bool unscaled)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);

        if (spellSlotSystem != null && spellSlotSystem.SpellCastList != null)
            spellSlotSystem.SpellCastList.SetActive(false);
    }
}
