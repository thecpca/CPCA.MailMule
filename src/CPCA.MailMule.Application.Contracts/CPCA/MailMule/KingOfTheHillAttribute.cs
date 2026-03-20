// CPCA MailMule
// Copyright (C) 2026 Doug Wilson
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU Affero General Public License as published by the Free Software Foundation, either
// version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License along with this
// program. If not, see <https://www.gnu.org/licenses/>.

namespace CPCA.MailMule;

/// <summary>
/// Marks a Blazor page as requiring "King of the Hill" exclusivity within the specified <see cref="Kingdom"/>.
/// Only one user can be the active operator (king) for a given kingdom at a time.
/// The <see cref="Components.SessionMonitor"/> component automatically detects this attribute
/// and enforces the exclusivity rules.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class KingOfTheHillAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KingOfTheHillAttribute"/> class.
    /// </summary>
    /// <param name="kingdom">The kingdom this page belongs to.</param>
    public KingOfTheHillAttribute(Kingdom kingdom)
    {
        Kingdom = kingdom;
    }

    /// <summary>
    /// The kingdom this page belongs to. Only one user can be king of a given kingdom at a time.
    /// </summary>
    public Kingdom Kingdom { get; }
}
