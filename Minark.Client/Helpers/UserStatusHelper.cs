using System.Windows.Media;
using Minark.Shared.Packets;

namespace Minark.Client.Helpers;

/// <summary>
///     Source unique de vérité pour le texte et la couleur associés à un UserStatus.
///     Tous les Converters et ViewModels délèguent ici.
/// </summary>
public static class UserStatusHelper
{
    extension(UserStatus status)
    {
        public string ToText()
        {
            return status switch
            {
                UserStatus.Online => "En ligne",
                UserStatus.Away => "Absent",
                UserStatus.Busy => "Occupé",
                _ => "Hors ligne"
            };
        }

        /// <summary>Retourne la couleur hex ARGB associée au statut.</summary>
        public string ToHex()
        {
            return status switch
            {
                UserStatus.Online => "#FF57CBB5",
                UserStatus.Away => "#FFFFC107",
                UserStatus.Busy => "#FFFF5252",
                _ => "#FF8F98A0"
            };
        }

        public Color ToColor()
        {
            return (Color)ColorConverter.ConvertFromString(status.ToHex());
        }

        public SolidColorBrush ToBrush()
        {
            return new SolidColorBrush(status.ToColor());
        }
    }
}