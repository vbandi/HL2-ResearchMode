using System;
using Microsoft.MixedReality.Toolkit.UI;
using UniRx;
using UnityEngine;

public static class InteractableHelper
{
    public static void BindTo(this Interactable interactable, ReactiveProperty<bool> property,
        IObservable<bool> isEnabledSource = null)
    {
        if (interactable.ButtonMode != SelectionModes.Toggle)
        {
            Debug.LogError("Bool ReactiveProperties can only be bound to toggles");
            return;
        }

        interactable.OnClick.AddListener(() => property.Value = interactable.IsToggled);
        var subscription = property.SubscribeOnMainThread().Subscribe(b => interactable.IsToggled = b);
        subscription.AddTo(interactable);

        isEnabledSource?.SubscribeOnMainThread().Subscribe(b => interactable.IsEnabled = b).AddTo(interactable);
    }

    public static void BindTo(this Interactable interactable, Action action,
        IObservable<bool> isEnabledSource = null)
    {
        interactable.OnClick.AddListener(action.Invoke);
        isEnabledSource?.SubscribeOnMainThread().Subscribe(b => interactable.IsEnabled = b).AddTo(interactable);
    }

    public static IObservable<bool> ObserveIsToggled(this Interactable interactable)
    {
        return interactable.ObserveEveryValueChanged(t => t.IsToggled);
    }

    public static IObservable<int> ObserveCurrentDimension(this Interactable interactable)
    {
        return interactable.ObserveEveryValueChanged(t => t.CurrentDimension);
    }
}