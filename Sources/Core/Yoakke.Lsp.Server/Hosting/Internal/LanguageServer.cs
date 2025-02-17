// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Yoakke.Lsp.Server.Internal;

namespace Yoakke.Lsp.Server.Hosting.Internal
{
    /// <summary>
    /// A default <see cref="ILanguageServer"/> implementation.
    /// </summary>
    internal class LanguageServer : ILanguageServer
    {
        private readonly IHost host;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageServer"/> class.
        /// </summary>
        /// <param name="host">The <see cref="IHost"/> that hosts this server.</param>
        public LanguageServer(IHost host)
        {
            this.host = host;
        }

        /// <inheritdoc/>
        public void Dispose() => this.host.Dispose();

        /// <inheritdoc/>
        public void Start() => this.host.Start();

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken) => this.host.StartAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<int> StopAsync(CancellationToken cancellationToken)
        {
            var lspService = (LanguageServerService?)this.host.Services.GetService(typeof(LanguageServerService))!;
            await lspService.StopAsync(cancellationToken);
            return lspService.ExitCode;
        }
    }
}
