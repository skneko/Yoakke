// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System.Collections.Generic;
using Newtonsoft.Json;
using Yoakke.Lsp.Model.Basic;
using Yoakke.Lsp.Model.LanguageFeatures;

namespace Yoakke.Lsp.Model.Capabilities.Server
{
    /// <summary>
    /// See https://microsoft.github.io/language-server-protocol/specifications/specification-current/#codeActionOptions.
    /// </summary>
    public class CodeActionOptions : IWorkDoneProgressOptions
    {
        /// <inheritdoc/>
        [JsonProperty("workDoneProgress", NullValueHandling = NullValueHandling.Ignore)]
        public bool? WorkDoneProgress { get; set; }

        /// <summary>
        /// CodeActionKinds that this server may return.
        ///
        /// The list of kinds may be generic, such as `CodeActionKind.Refactor`,
        /// or the server may list out every specific kind they provide.
        /// </summary>
        [JsonProperty("codeActionKinds", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<CodeActionKind>? CodeActionKinds { get; set; }

        /// <summary>
        /// The server provides support to resolve additional
        /// information for a code action.
        /// </summary>
        [Since(3, 16, 0)]
        [JsonProperty("resolveProvider", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ResolveProvider { get; set; }
    }
}
