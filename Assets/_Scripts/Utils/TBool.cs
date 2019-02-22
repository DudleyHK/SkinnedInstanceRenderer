




public struct TBool
{
    private readonly byte value;

    public TBool(bool _val)
    {
        value = (byte)(_val ? 1 : 0);
    }

    public static implicit operator TBool(bool _val) { return new TBool(_val); }
    public static implicit operator bool(TBool _val) { return _val.value != 0; }
}