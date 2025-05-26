import * as jspb from 'google-protobuf'



export class DiscordTokenGrpc extends jspb.Message {
  getId(): string;
  setId(value: string): DiscordTokenGrpc;

  getUserId(): string;
  setUserId(value: string): DiscordTokenGrpc;

  getName(): string;
  setName(value: string): DiscordTokenGrpc;

  getToken(): string;
  setToken(value: string): DiscordTokenGrpc;

  getUpdatedAt(): string;
  setUpdatedAt(value: string): DiscordTokenGrpc;

  getCreatedAt(): string;
  setCreatedAt(value: string): DiscordTokenGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): DiscordTokenGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: DiscordTokenGrpc): DiscordTokenGrpc.AsObject;
  static serializeBinaryToWriter(message: DiscordTokenGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): DiscordTokenGrpc;
  static deserializeBinaryFromReader(message: DiscordTokenGrpc, reader: jspb.BinaryReader): DiscordTokenGrpc;
}

export namespace DiscordTokenGrpc {
  export type AsObject = {
    id: string,
    userId: string,
    name: string,
    token: string,
    updatedAt: string,
    createdAt: string,
  }
}

export class DiscordTokenSearchOptionsGrpc extends jspb.Message {
  getIdsList(): Array<string>;
  setIdsList(value: Array<string>): DiscordTokenSearchOptionsGrpc;
  clearIdsList(): DiscordTokenSearchOptionsGrpc;
  addIds(value: string, index?: number): DiscordTokenSearchOptionsGrpc;

  getUserIdsList(): Array<string>;
  setUserIdsList(value: Array<string>): DiscordTokenSearchOptionsGrpc;
  clearUserIdsList(): DiscordTokenSearchOptionsGrpc;
  addUserIds(value: string, index?: number): DiscordTokenSearchOptionsGrpc;

  getNamesList(): Array<string>;
  setNamesList(value: Array<string>): DiscordTokenSearchOptionsGrpc;
  clearNamesList(): DiscordTokenSearchOptionsGrpc;
  addNames(value: string, index?: number): DiscordTokenSearchOptionsGrpc;

  getTokensList(): Array<string>;
  setTokensList(value: Array<string>): DiscordTokenSearchOptionsGrpc;
  clearTokensList(): DiscordTokenSearchOptionsGrpc;
  addTokens(value: string, index?: number): DiscordTokenSearchOptionsGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): DiscordTokenSearchOptionsGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: DiscordTokenSearchOptionsGrpc): DiscordTokenSearchOptionsGrpc.AsObject;
  static serializeBinaryToWriter(message: DiscordTokenSearchOptionsGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): DiscordTokenSearchOptionsGrpc;
  static deserializeBinaryFromReader(message: DiscordTokenSearchOptionsGrpc, reader: jspb.BinaryReader): DiscordTokenSearchOptionsGrpc;
}

export namespace DiscordTokenSearchOptionsGrpc {
  export type AsObject = {
    idsList: Array<string>,
    userIdsList: Array<string>,
    namesList: Array<string>,
    tokensList: Array<string>,
  }
}

export class DiscordTokenListGrpc extends jspb.Message {
  getItemsList(): Array<DiscordTokenGrpc>;
  setItemsList(value: Array<DiscordTokenGrpc>): DiscordTokenListGrpc;
  clearItemsList(): DiscordTokenListGrpc;
  addItems(value?: DiscordTokenGrpc, index?: number): DiscordTokenGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): DiscordTokenListGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: DiscordTokenListGrpc): DiscordTokenListGrpc.AsObject;
  static serializeBinaryToWriter(message: DiscordTokenListGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): DiscordTokenListGrpc;
  static deserializeBinaryFromReader(message: DiscordTokenListGrpc, reader: jspb.BinaryReader): DiscordTokenListGrpc;
}

export namespace DiscordTokenListGrpc {
  export type AsObject = {
    itemsList: Array<DiscordTokenGrpc.AsObject>,
  }
}

export class DiscordTokenResultGrpc extends jspb.Message {
  getItemsList(): Array<DiscordTokenGrpc>;
  setItemsList(value: Array<DiscordTokenGrpc>): DiscordTokenResultGrpc;
  clearItemsList(): DiscordTokenResultGrpc;
  addItems(value?: DiscordTokenGrpc, index?: number): DiscordTokenGrpc;

  getErrorsList(): Array<string>;
  setErrorsList(value: Array<string>): DiscordTokenResultGrpc;
  clearErrorsList(): DiscordTokenResultGrpc;
  addErrors(value: string, index?: number): DiscordTokenResultGrpc;

  getIsSucceeded(): boolean;
  setIsSucceeded(value: boolean): DiscordTokenResultGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): DiscordTokenResultGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: DiscordTokenResultGrpc): DiscordTokenResultGrpc.AsObject;
  static serializeBinaryToWriter(message: DiscordTokenResultGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): DiscordTokenResultGrpc;
  static deserializeBinaryFromReader(message: DiscordTokenResultGrpc, reader: jspb.BinaryReader): DiscordTokenResultGrpc;
}

export namespace DiscordTokenResultGrpc {
  export type AsObject = {
    itemsList: Array<DiscordTokenGrpc.AsObject>,
    errorsList: Array<string>,
    isSucceeded: boolean,
  }
}

export class EditRequestGrpc extends jspb.Message {
  getTarget(): DiscordTokenGrpc | undefined;
  setTarget(value?: DiscordTokenGrpc): EditRequestGrpc;
  hasTarget(): boolean;
  clearTarget(): EditRequestGrpc;

  getModify(): DiscordTokenGrpc | undefined;
  setModify(value?: DiscordTokenGrpc): EditRequestGrpc;
  hasModify(): boolean;
  clearModify(): EditRequestGrpc;

  getOverrideFieldsList(): Array<string>;
  setOverrideFieldsList(value: Array<string>): EditRequestGrpc;
  clearOverrideFieldsList(): EditRequestGrpc;
  addOverrideFields(value: string, index?: number): EditRequestGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): EditRequestGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: EditRequestGrpc): EditRequestGrpc.AsObject;
  static serializeBinaryToWriter(message: EditRequestGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): EditRequestGrpc;
  static deserializeBinaryFromReader(message: EditRequestGrpc, reader: jspb.BinaryReader): EditRequestGrpc;
}

export namespace EditRequestGrpc {
  export type AsObject = {
    target?: DiscordTokenGrpc.AsObject,
    modify?: DiscordTokenGrpc.AsObject,
    overrideFieldsList: Array<string>,
  }
}

export class EditBatchRequestGrpc extends jspb.Message {
  getTargetsList(): Array<DiscordTokenGrpc>;
  setTargetsList(value: Array<DiscordTokenGrpc>): EditBatchRequestGrpc;
  clearTargetsList(): EditBatchRequestGrpc;
  addTargets(value?: DiscordTokenGrpc, index?: number): DiscordTokenGrpc;

  getModify(): DiscordTokenGrpc | undefined;
  setModify(value?: DiscordTokenGrpc): EditBatchRequestGrpc;
  hasModify(): boolean;
  clearModify(): EditBatchRequestGrpc;

  getOverrideFieldsList(): Array<string>;
  setOverrideFieldsList(value: Array<string>): EditBatchRequestGrpc;
  clearOverrideFieldsList(): EditBatchRequestGrpc;
  addOverrideFields(value: string, index?: number): EditBatchRequestGrpc;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): EditBatchRequestGrpc.AsObject;
  static toObject(includeInstance: boolean, msg: EditBatchRequestGrpc): EditBatchRequestGrpc.AsObject;
  static serializeBinaryToWriter(message: EditBatchRequestGrpc, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): EditBatchRequestGrpc;
  static deserializeBinaryFromReader(message: EditBatchRequestGrpc, reader: jspb.BinaryReader): EditBatchRequestGrpc;
}

export namespace EditBatchRequestGrpc {
  export type AsObject = {
    targetsList: Array<DiscordTokenGrpc.AsObject>,
    modify?: DiscordTokenGrpc.AsObject,
    overrideFieldsList: Array<string>,
  }
}

