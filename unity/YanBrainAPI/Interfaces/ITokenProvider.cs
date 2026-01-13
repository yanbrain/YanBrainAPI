// YanBrainAPI/Interfaces/ITokenProvider.cs - ADD PROPERTIES

using System;
using System.Threading.Tasks;

namespace YanBrainAPI.Interfaces
{
    public interface ITokenProvider
    {
        Task<string> GetTokenAsync();
        
        /// <summary>
        /// Check if user is currently authenticated
        /// </summary>
        bool IsAuthenticated { get; }
        
        /// <summary>
        /// Fired when authentication state changes (login/logout)
        /// </summary>
        event Action OnAuthenticationChanged;
    }
}