pipeline {
  agent any
  stages {
    stage('restore') {
      steps {
        bat 'dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj'
      }
    }

    stage('build') {
      parallel {
        stage('win10single') {
          steps {
            bat 'publish_custom.bat win10-x64 single false true'
          }
        }

        stage('win10merged') {
          steps {
            bat 'publish_custom.bat win10-x64 merged true true'
          }
        }

        stage('osxmerged') {
          steps {
            bat 'publish_custom.bat osx-x64 merged true false'
          }
        }

        stage('linuxmerged') {
          steps {
            bat 'publish_custom.bat linux-x64 merged true false'
          }
        }

      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
      }
    }
  }
}