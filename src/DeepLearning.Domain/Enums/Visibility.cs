namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// 成员名用PascalCase(Private/Shared),经snake_case转换后对应
    /// visibility_enum的label('private'等);'private'是C#保留字,不能直接当枚举成员名。
    /// </summary>
    public enum Visibility
    {
        Private,
        Shared
    }
}
