namespace OpticaSosa.Domain.Business;

public sealed class BusinessSettings
{
    public int Id { get; private set; } = 1;
    public BusinessProfile Profile { get; private set; } = new(null, null, null, null, null, null, null, null, null);
    public byte[]? LogoPng { get; private set; }
    public Guid? LogoVersion { get; private set; }
    public Guid Version { get; private set; }

    public void Update(BusinessProfile profile, byte[]? normalizedLogo, bool removeLogo)
    {
        Profile = profile.ValidateAndNormalize();
        if (removeLogo) { LogoPng = null; LogoVersion = null; }
        else if (normalizedLogo is not null)
        {
            LogoPng = normalizedLogo;
            LogoVersion = Guid.NewGuid();
        }
        Version = Guid.NewGuid();
    }
}
