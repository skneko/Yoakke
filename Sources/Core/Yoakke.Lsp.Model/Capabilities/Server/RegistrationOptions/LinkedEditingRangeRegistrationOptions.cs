// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System.Collections.Generic;
using Newtonsoft.Json;
using Yoakke.Lsp.Model.Basic;

namespace Yoakke.Lsp.Model.Capabilities.Server.RegistrationOptions
{
    /// <summary>
    /// See https://microsoft.github.io/language-server-protocol/specifications/specification-current/#linkedEditingRangeRegistrationOptions.
    /// </summary>
    public class LinkedEditingRangeRegistrationOptions : LinkedEditingRangeOptions, ITextDocumentRegistrationOptions, IStaticRegistrationOptions
    {
        /// <inheritdoc/>
        [JsonProperty("documentSelector")]
        public IReadOnlyList<DocumentFilter>? DocumentSelector { get; set; }

        /// <inheritdoc/>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }
    }
}
