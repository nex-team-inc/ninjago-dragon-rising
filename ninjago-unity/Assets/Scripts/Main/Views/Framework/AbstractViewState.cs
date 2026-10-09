using System;
using System.Threading;

namespace Nex
{
    /// <summary>
    /// A view state allow any view to store some state so that they can be recovered when switching scenes.
    /// Each view can have its own view state implementation.
    /// View states can also be chained in a singly linked list. That mimics the concept of a navigation stack.
    /// Technically, not all views have view states, and views that do not need to be restored will likely not
    /// have one.
    /// </summary>
    public abstract class AbstractViewState : IDisposable
    {
        // The identifier of the view that has this view state.
        public abstract View.ViewIdentifier ViewIdentifier { get; }

        // This is the base class of all the view states to be stored inside DataPasser.
        readonly CancellationTokenSource tokenSource = new();
        protected CancellationToken CancellationToken => tokenSource.Token;

        AbstractViewState nextViewState;
        public AbstractViewState NextViewState
        {
            get => nextViewState;
            set
            {
                nextViewState?.Dispose();
                nextViewState = value;
            }
        }

        public bool HasValidNextViewStateOrClear(View.ViewIdentifier identifier)
        {
            if (nextViewState == null) return false;  // This signals invalid.
            if (nextViewState.ViewIdentifier == identifier) return true;
            NextViewState = null;
            return false;
        }

        // This checks two identifiers, and return the (first) one that matches.
        public View.ViewIdentifier HasValidNextViewStateOrClear(View.ViewIdentifier identifier1, View.ViewIdentifier identifier2)
        {
            if (nextViewState == null) return View.ViewIdentifier.Invalid;
            if (nextViewState.ViewIdentifier == identifier1) return identifier1;
            if (nextViewState.ViewIdentifier == identifier2) return identifier2;
            NextViewState = null;
            return View.ViewIdentifier.Invalid;
        }

        // This checks three identifiers, and return the (first) one that matches.
        public View.ViewIdentifier HasValidNextViewStateOrClear(
            View.ViewIdentifier identifier1, View.ViewIdentifier identifier2, View.ViewIdentifier identifier3)
        {
            if (nextViewState == null) return View.ViewIdentifier.Invalid;
            if (nextViewState.ViewIdentifier == identifier1) return identifier1;
            if (nextViewState.ViewIdentifier == identifier2) return identifier2;
            if (nextViewState.ViewIdentifier == identifier3) return identifier3;
            NextViewState = null;
            return View.ViewIdentifier.Invalid;
        }

        public virtual void Dispose()
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
            nextViewState?.Dispose();
        }
    }
}
