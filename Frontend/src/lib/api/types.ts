export interface User {
    id: string;
    username: string;
}

export interface LoginCredentials {
    username: string;
    password: string;
}

export interface RegisterData {
    name: string;
    username: string;
    password: string;
}

export interface ErrorItem {
    code: string;
    description: string;
}

export interface ErrorResponse {
    [key: string]: ErrorItem[];
}
