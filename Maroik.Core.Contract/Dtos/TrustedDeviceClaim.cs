namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// What a browser's trusted-device cookie says, after the web layer has verified its signature: the account it was
/// issued to, that account's device stamp at the time, and when it was issued. Whether it still makes the browser a
/// trusted device is the account's decision (<c>Account.TrustsDevice</c>).
/// </summary>
public sealed record TrustedDeviceClaim(string Email, string DeviceStamp, DateTime IssuedAt);
