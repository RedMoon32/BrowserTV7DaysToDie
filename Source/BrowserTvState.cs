public sealed class BrowserTvState
{
    public BrowserTvPowerState Power = BrowserTvPowerState.Off;
    public Vector3i BlockPos = Vector3i.zero;
    public string SessionId = "";
    public string BridgeEndpoint = "";
    public string StreamUrl = "";
    public string ViewerToken = "";
    public string ControllerToken = "";
    public int ControllerEntityId = -1;
    public string CurrentUrl = "";
    public float Volume = 0.5f;
    public string StatusText = "Off";
    public int Revision;

    public BrowserTvState Clone()
    {
        return (BrowserTvState)MemberwiseClone();
    }

    public bool IsSameTv(Vector3i pos)
    {
        return BlockPos.x == pos.x && BlockPos.y == pos.y && BlockPos.z == pos.z;
    }

    public void Reset()
    {
        Power = BrowserTvPowerState.Off;
        BlockPos = Vector3i.zero;
        SessionId = "";
        BridgeEndpoint = "";
        StreamUrl = "";
        ViewerToken = "";
        ControllerToken = "";
        ControllerEntityId = -1;
        CurrentUrl = "";
        Volume = 0.5f;
        StatusText = "Off";
        Revision++;
    }
}
