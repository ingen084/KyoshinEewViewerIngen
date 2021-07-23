pipeline {
  agent any
  stages {
    stage('buld win10single') {
      steps {
        bat 'publish_custom.bat Windows win10-x64 single false'
      }
    }

    stage('buld win10merged') {
      steps {
        bat 'publish_custom.bat Windows win10-x64 merged true'
      }
    }

    stage('buld osxmerged') {
      steps {
        bat 'publish_custom.bat Linux osx-x64 merged true'
      }
    }

    stage('buld linuxmerged') {
      steps {
        bat 'publish_custom.bat Linux linux-x64 merged true'
      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)

        withCredentials([string(credentialsId: 'DISCORD_WEBHOOK', variable: 'WebhookUrl')]) {
          discordSend(description: "build completed!", footer: "Jenkins", link: env.BUILD_URL, result: currentBuild.currentResult, title: "${env.JOB_NAME}#${env.BUILD_NUMBER}", webhookURL: WebhookUrl)
        }
      }
    }
  }
}