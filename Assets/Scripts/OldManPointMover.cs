using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class OldManPointMover : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Transform point1;
    [SerializeField] private Transform point2;
    [SerializeField] private float moveSpeed = 0.8f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private bool faceMainCameraOnArrive = true;

    [Header("Animator Parameters")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameterName = "walking";
    [SerializeField] private bool playIdleOnStart = true;

    private bool keepFacingMainCamera;
    private Coroutine moveRoutine;

    private void Awake()
    {
        if (!animator)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        StartTalk( Vector3.zero);
        //if (playIdleOnStart)
        //{
        //    SetWalking(false);
        //    MoveFromPoint1ToPoint2();
        //}
    }

    [ContextMenu("Move From Point1 To Point2")]
    //public void MoveFromPoint1ToPoint2()
    //{
    //    if (!point1 || !point2)
    //    {
    //        Debug.LogWarning("[OldManPointMover] Point1 or Point2 is not assigned.", this);
    //        return;
    //    }

    //    transform.position = point1.position;
    //    keepFacingMainCamera = false;
    //    MoveToPoint2();
    //}

    //public void MoveToPoint2()
    //{
    //    if (!point2)
    //    {
    //        Debug.LogWarning("[OldManPointMover] Point2 is not assigned.", this);
    //        return;
    //    }

    //    if (moveRoutine != null)
    //    {
    //        StopCoroutine(moveRoutine);
    //    }

    //    keepFacingMainCamera = false;
    //    moveRoutine = StartCoroutine(MoveToPoint(point2.position));
    //}

    public void StopMove()
    {
        //if (moveRoutine != null)
        //{
        //    StopCoroutine(moveRoutine);
        //    moveRoutine = null;
        //}

        SetWalking(false);
        keepFacingMainCamera = faceMainCameraOnArrive;
    }

    private void LateUpdate()
    {
        if (keepFacingMainCamera)
        {
            FaceMainCamera();
        }
    }

    private IEnumerator MoveToPoint(Vector3 targetPosition)
    {
        SetWalking(true);

        while ((transform.position - targetPosition).sqrMagnitude > 0.0001f)
        {
            Vector3 currentPosition = transform.position;
            Vector3 toTarget = targetPosition - currentPosition;
            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);

            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }
        StartTalk(targetPosition);
    }

    private void StartTalk(Vector3 targetPosition)
    {
        //transform.position = targetPosition;
        moveRoutine = null;
        SetWalking(false);
        keepFacingMainCamera = faceMainCameraOnArrive;
        FaceMainCamera();
        VoiceChatManager.Instance.StartRecordingFromExternal();
    }

    private void SetWalking(bool isWalking)
    {
        if (!animator || string.IsNullOrEmpty(walkingParameterName))
        {
            return;
        }

        animator.applyRootMotion = false;
        animator.SetBool(walkingParameterName, isWalking);
    }

    private void FaceMainCamera()
    {
        if (!faceMainCameraOnArrive || !Camera.main)
        {
            return;
        }

        Vector3 toCamera = Camera.main.transform.position - transform.position;
        Vector3 flatDirection = new Vector3(toCamera.x, 0f, toCamera.z);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
    }
}
