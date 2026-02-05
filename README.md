# EchoPhase

## Introduction

EchoPhase is a platform for managing bots and integrating with various API services.
It allows you to easily automate tasks, connect different services, and manage the bot lifecycle.

## Commands

### Frontend

**Build Docker image:**
```
$ npm run docker:build
```

**Tag Docker image:**
```
$ npm run docker:tag
```

**Push Docker image:**
```
$ npm run docker:push
```

**All Docker commands at once:**
```
$ npm run docker:publish
```

#### Configuration

**Package variables:**
- dockerImage
    - echophase-frontend
- dockerRegistry
    - localhost:5000
- dockerTag
    - latest

### Backend

**Build Docker, tag and push:**
```
$ dotnet msbuild -t:DockerBuildAndPush
```

#### Configuration

**MSBuild variables:**
- DockerImageName
    - echophase
- DockerRegistry
    - localhost:5000
- DockerTag
    - latest

### Publishing images

Before continuing, make sure you have built the images and pushed them to any registry of your choice, since at the moment the images are not publicly available.
It’s best to do this locally _(default build)_.

**Launching local registry:**
```
$ docker run registry:2 
```

### Launching cluster

**Launching default Helm:**
```
helm install echophase helm --namespace echophase --create-namespace
```

**Launching default Docker Compose:**
```
cd compose; docker-compose up -d
```
