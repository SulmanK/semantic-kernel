# Copyright (c) Microsoft. All rights reserved.


from semantic_kernel.utils.experimental_decorator import experimental_class


@experimental_class
class RestApiOperationExpectedResponse:
    def __init__(self, description: str, media_type: str, schema: str | None = None):
        self.description = description
        self.media_type = media_type
        self.schema = schema
