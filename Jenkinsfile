pipeline {
  agent any
  stages {
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

      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
      }
    }

  }
}