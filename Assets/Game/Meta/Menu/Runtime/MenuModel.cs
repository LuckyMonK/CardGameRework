namespace CardGame.Meta.Menu
{
    public sealed class MenuModel
    {
        public bool IsBusy { get; private set; }
        public string Error { get; private set; } = "";

        public void BeginAction()
        {
            IsBusy = true;
            Error = "";
        }

        public void CompleteAction(string error)
        {
            IsBusy = false;
            Error = error ?? "";
        }
    }
}
